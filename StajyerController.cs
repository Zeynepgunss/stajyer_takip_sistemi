using BusinessLayer;
using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Controllers
{
    [Authorize(Roles = "Stajyer")]
    public class StajyerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPerformansHesaplamaService _performansService;

        public StajyerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPerformansHesaplamaService performansService)
        {
            _context = context;
            _userManager = userManager;
            _performansService = performansService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .Include(s => s.Birim)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Eğer stajyer kaydı yoksa, otomatik oluştur
            if (stajyer == null)
            {
                // Varsayılan birim bul (yoksa ilk birimi al)
                var varsayilanBirim = await _context.Birimler
                    .Where(b => b.Aktif)
                    .FirstOrDefaultAsync();

                if (varsayilanBirim == null)
                {
                    return View("StajyerKayitYok");
                }

                stajyer = new Stajyer
                {
                    UserId = user.Id,
                    BirimId = varsayilanBirim.Id,
                    BaslangicTarihi = user.OlusturmaTarihi ?? DateTime.Now,
                    BitisTarihi = (user.OlusturmaTarihi ?? DateTime.Now).AddMonths(3),
                    ToplamGun = 0,
                    TamamlananGun = 0,
                    DevamOrani = 0,
                    Aktif = true,
                    OlusturmaTarihi = DateTime.Now
                };

                _context.Stajyerler.Add(stajyer);
                await _context.SaveChangesAsync();

                // Navigation property'leri yükle
                stajyer = await _context.Stajyerler
                    .Include(s => s.Birim)
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);
            }

            // Tarih kontrolü - eğer tarihler boşsa veya geçersizse hesaplama yapma
            var tarihlerGecerli = stajyer.BaslangicTarihi != default(DateTime) && stajyer.BaslangicTarihi.Year >= 2000 &&
                                  stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000;

            int kalanGun = 0;
            int toplamIsGunu = 0;
            int tamamlananIsGunu = 0;
            decimal devamOrani = 0;

            if (tarihlerGecerli)
            {
                // Toplam iş günü hesaplama (hafta sonları hariç) - Bilgilerim sayfasıyla aynı
                var tarih = stajyer.BaslangicTarihi;
                while (tarih <= stajyer.BitisTarihi)
                {
                    if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                    {
                        toplamIsGunu++;
                        // Bugüne kadar olan iş günleri
                        if (tarih <= DateTime.Now)
                        {
                            tamamlananIsGunu++;
                        }
                    }
                    tarih = tarih.AddDays(1);
                }

                // Kalan iş günü hesaplama (hafta sonları hariç)
                var bugun = DateTime.Now.Date;
                if (bugun < stajyer.BitisTarihi)
                {
                    var kalanTarih = bugun.AddDays(1);
                    while (kalanTarih <= stajyer.BitisTarihi)
                    {
                        if (kalanTarih.DayOfWeek != DayOfWeek.Saturday && kalanTarih.DayOfWeek != DayOfWeek.Sunday)
                        {
                            kalanGun++;
                        }
                        kalanTarih = kalanTarih.AddDays(1);
                    }
                }

                // Devam oranı hesaplama
                if (toplamIsGunu > 0)
                {
                    devamOrani = (decimal)(tamamlananIsGunu * 100.0 / toplamIsGunu);
                }

                // Veritabanına kaydet
                stajyer.ToplamGun = toplamIsGunu;
                stajyer.TamamlananGun = tamamlananIsGunu;
                stajyer.DevamOrani = devamOrani;
                await _context.SaveChangesAsync();
            }

            // Son görevler
            var sonGorevler = await _context.GunlukGorevler
                .Where(g => g.StajyerId == stajyer.Id)
                .OrderByDescending(g => g.Tarih)
                .Take(5)
                .ToListAsync();

            // Performans skoru - sadece staj bitmişse ve sorumlu puan vermişse göster
            PerformansSkoru? sonPerformans = null;
            var stajBitti = stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000 && DateTime.Now >= stajyer.BitisTarihi;
            if (stajBitti)
            {
                sonPerformans = await _context.PerformansSkorlari
                    .Where(p => p.StajyerId == stajyer.Id && p.SorumluFinalPuanı.HasValue)
                    .OrderByDescending(p => p.HesaplamaTarihi)
                    .FirstOrDefaultAsync();
            }

            var manuelGorevler = await _context.ManuelGorevler
                .Where(m => m.UserId == user.Id)
                .OrderByDescending(m => m.OlusturmaTarihi)
                .Take(10)
                .ToListAsync();

            ViewBag.KalanGun = kalanGun;
            ViewBag.ToplamIsGunu = toplamIsGunu;
            ViewBag.TamamlananIsGunu = tamamlananIsGunu;
            ViewBag.SonGorevler = sonGorevler;
            ViewBag.SonPerformans = sonPerformans;
            ViewBag.ManuelGorevler = manuelGorevler;

            return View(stajyer);
        }

        [HttpGet]
        public IActionResult GunlukGorevEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GunlukGorevEkle(GunlukGorev gorev, IFormFile? dosya)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null)
            {
                ModelState.AddModelError("", "Stajyer bilgisi bulunamadı.");
                return View(gorev);
            }

            // Aynı tarihte görev var mı kontrol et
            var mevcutGorev = await _context.GunlukGorevler
                .FirstOrDefaultAsync(g => g.StajyerId == stajyer.Id && g.Tarih.Date == gorev.Tarih.Date);

            if (mevcutGorev != null)
            {
                ModelState.AddModelError("Tarih", "Bu tarih için zaten bir görev kaydı var.");
                return View(gorev);
            }

            // Geç teslim kontrolü (bugünden önceki tarihler için)
            if (gorev.Tarih.Date < DateTime.Now.Date)
            {
                gorev.GecTeslim = true;
            }

            // Dosya yükleme
            if (dosya != null && dosya.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "gorevler");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{dosya.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dosya.CopyToAsync(stream);
                }

                gorev.DosyaYolu = $"/uploads/gorevler/{uniqueFileName}";
                gorev.DosyaAdi = dosya.FileName;
            }

            gorev.StajyerId = stajyer.Id;
            gorev.OlusturmaTarihi = DateTime.Now;

            _context.GunlukGorevler.Add(gorev);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Günlük görev başarıyla eklendi.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Gorevlerim()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null) return NotFound();

            var gorevler = await _context.GunlukGorevler
                .Where(g => g.StajyerId == stajyer.Id)
                .OrderByDescending(g => g.Tarih)
                .ToListAsync();

            return View(gorevler);
        }

        [HttpGet]
        public async Task<IActionResult> Performans()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .Include(s => s.Projeler)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null) return NotFound();

            // Haftalık performans hesapla (sadece staj bitmişse)
            List<PerformansSkoru> haftalikPerformans = new List<PerformansSkoru>();
            var stajBitti = stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000 && DateTime.Now >= stajyer.BitisTarihi;
            
            // Son performans skoru - sadece staj bitmişse ve sorumlu puan vermişse göster
            PerformansSkoru? sonPerformans = null;
            if (stajBitti)
            {
                sonPerformans = await _context.PerformansSkorlari
                    .Where(p => p.StajyerId == stajyer.Id && p.SorumluFinalPuanı.HasValue)
                    .OrderByDescending(p => p.HesaplamaTarihi)
                    .FirstOrDefaultAsync();
            }

            ViewBag.HaftalikPerformans = haftalikPerformans;
            ViewBag.SonPerformans = sonPerformans;

            return View(stajyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProjeEkle(string projeAdi, string? githubLink, string? kullanilanDil)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null)
            {
                TempData["Error"] = "Stajyer kaydı bulunamadı.";
                return RedirectToAction("Performans");
            }

            if (string.IsNullOrWhiteSpace(projeAdi))
            {
                TempData["Error"] = "Proje adı zorunludur.";
                return RedirectToAction("Performans");
            }

            var proje = new StajyerProje
            {
                StajyerId = stajyer.Id,
                ProjeAdi = projeAdi.Trim(),
                GitHubLink = string.IsNullOrWhiteSpace(githubLink) ? null : githubLink.Trim(),
                KullanilanDil = string.IsNullOrWhiteSpace(kullanilanDil) ? null : kullanilanDil.Trim(),
                OlusturmaTarihi = DateTime.Now
            };

            _context.StajyerProjeler.Add(proje);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Proje başarıyla eklendi.";
            return RedirectToAction("Performans");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProjeSil(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null) return NotFound();

            var proje = await _context.StajyerProjeler
                .FirstOrDefaultAsync(p => p.Id == id && p.StajyerId == stajyer.Id);

            if (proje == null)
            {
                TempData["Error"] = "Proje bulunamadı veya size ait değil.";
                return RedirectToAction("Performans");
            }

            _context.StajyerProjeler.Remove(proje);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Proje silindi.";
            return RedirectToAction("Performans");
        }

        [HttpGet]
        public async Task<IActionResult> Yeteneklerim()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy.Yetenek)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null) return NotFound();

            var tumYetenekler = await _context.Yetenekler
                .Where(y => y.Aktif)
                .ToListAsync();

            ViewBag.TumYetenekler = tumYetenekler;

            return View(stajyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetenekEkle(int? yetenekId, int seviye, string? yetenekAdi)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null) return NotFound();

            int finalYetenekId;

            // Manuel yetenek ekleme
            if (!yetenekId.HasValue && !string.IsNullOrWhiteSpace(yetenekAdi))
            {
                // Aynı isimde yetenek var mı kontrol et
                var mevcutYetenekVar = await _context.Yetenekler
                    .FirstOrDefaultAsync(y => y.Ad.ToLower().Trim() == yetenekAdi.ToLower().Trim() && y.Aktif);

                if (mevcutYetenekVar != null)
                {
                    finalYetenekId = mevcutYetenekVar.Id;
                }
                else
                {
                    // Yeni yetenek oluştur
                    var yeniYetenek = new Yetenek
                    {
                        Ad = yetenekAdi.Trim(),
                        Kategori = "Diğer", // Varsayılan kategori
                        Aktif = true
                    };
                    _context.Yetenekler.Add(yeniYetenek);
                    await _context.SaveChangesAsync();
                    finalYetenekId = yeniYetenek.Id;
                }
            }
            else if (yetenekId.HasValue)
            {
                finalYetenekId = yetenekId.Value;
            }
            else
            {
                TempData["Error"] = "Lütfen bir yetenek seçin veya yetenek adı girin.";
                return RedirectToAction("Yeteneklerim");
            }

            // Mevcut yetenek kontrolü
            var mevcutYetenek = await _context.StajyerYetenekler
                .FirstOrDefaultAsync(sy => sy.StajyerId == stajyer.Id && sy.YetenekId == finalYetenekId);

            if (mevcutYetenek != null)
            {
                mevcutYetenek.Seviye = seviye;
                mevcutYetenek.GuncellemeTarihi = DateTime.Now;
            }
            else
            {
                var stajyerYetenek = new StajyerYetenek
                {
                    StajyerId = stajyer.Id,
                    YetenekId = finalYetenekId,
                    Seviye = seviye,
                    OlusturmaTarihi = DateTime.Now
                };
                _context.StajyerYetenekler.Add(stajyerYetenek);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Yetenek başarıyla eklendi/güncellendi.";
            return RedirectToAction("Yeteneklerim");
        }

        [HttpGet]
        public async Task<IActionResult> Bilgilerim()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .Include(s => s.Birim)
                .Include(s => s.User)
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy.Yetenek)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Eğer stajyer kaydı yoksa, otomatik oluştur
            if (stajyer == null)
            {
                var varsayilanBirim = await _context.Birimler
                    .Where(b => b.Aktif)
                    .FirstOrDefaultAsync();

                if (varsayilanBirim == null)
                {
                    return View("StajyerKayitYok");
                }

                stajyer = new Stajyer
                {
                    UserId = user.Id,
                    BirimId = varsayilanBirim.Id,
                    BaslangicTarihi = user.OlusturmaTarihi ?? DateTime.Now,
                    BitisTarihi = (user.OlusturmaTarihi ?? DateTime.Now).AddMonths(3),
                    ToplamGun = 0,
                    TamamlananGun = 0,
                    DevamOrani = 0,
                    Aktif = true,
                    OlusturmaTarihi = DateTime.Now
                };

                _context.Stajyerler.Add(stajyer);
                await _context.SaveChangesAsync();

                stajyer = await _context.Stajyerler
                    .Include(s => s.Birim)
                    .Include(s => s.User)
                    .Include(s => s.Yetenekler)
                        .ThenInclude(sy => sy.Yetenek)
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);
            }

            // İş günü hesaplama (hafta sonları hariç) - sadece tarihler doluysa
            var toplamGun = 0;
            if (stajyer.BaslangicTarihi != default(DateTime) && stajyer.BaslangicTarihi.Year >= 2000 &&
                stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000)
            {
                var tarih = stajyer.BaslangicTarihi;
                while (tarih <= stajyer.BitisTarihi)
                {
                    if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                    {
                        toplamGun++;
                    }
                    tarih = tarih.AddDays(1);
                }
            }

            ViewBag.ToplamIsGunu = toplamGun;

            return View(stajyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EgitimBilgisiGuncelle(string? okul, string? bolum, string? sinif)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null)
            {
                TempData["Error"] = "Stajyer kaydı bulunamadı.";
                return RedirectToAction("Bilgilerim");
            }

            stajyer.Okul = okul?.Trim();
            stajyer.Bolum = bolum?.Trim();
            stajyer.Sinif = sinif?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Okul, bölüm ve sınıf bilgileriniz kaydedildi.";
            return RedirectToAction("Bilgilerim");
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .Include(s => s.Birim)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Eğer stajyer kaydı yoksa, otomatik oluştur
            if (stajyer == null)
            {
                var varsayilanBirim = await _context.Birimler
                    .Where(b => b.Aktif)
                    .FirstOrDefaultAsync();

                if (varsayilanBirim == null)
                {
                    return View("StajyerKayitYok");
                }

                stajyer = new Stajyer
                {
                    UserId = user.Id,
                    BirimId = varsayilanBirim.Id,
                    BaslangicTarihi = user.OlusturmaTarihi ?? DateTime.Now,
                    BitisTarihi = (user.OlusturmaTarihi ?? DateTime.Now).AddMonths(3),
                    ToplamGun = 0,
                    TamamlananGun = 0,
                    DevamOrani = 0,
                    Aktif = true,
                    OlusturmaTarihi = DateTime.Now
                };

                _context.Stajyerler.Add(stajyer);
                await _context.SaveChangesAsync();

                stajyer = await _context.Stajyerler
                    .Include(s => s.Birim)
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);
            }

            // Eğer başlangıç tarihi boşsa veya geçersizse, kullanıcının kayıt tarihini kullan
            if (stajyer.BaslangicTarihi == default(DateTime) || stajyer.BaslangicTarihi.Year < 2000)
            {
                if (user.OlusturmaTarihi.HasValue)
                {
                    stajyer.BaslangicTarihi = user.OlusturmaTarihi.Value;
                }
                else
                {
                    stajyer.BaslangicTarihi = DateTime.Now;
                }
            }

            // Birim listesini getir
            var birimler = await _context.Birimler
                .Where(b => b.Aktif)
                .OrderBy(b => b.Ad)
                .ToListAsync();

            ViewBag.Birimler = birimler;

            return View(stajyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(Stajyer model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (stajyer == null)
            {
                return NotFound();
            }

            // Başlangıç tarihi boşsa, kullanıcının kayıt tarihini kullan
            if (model.BaslangicTarihi == default(DateTime) || model.BaslangicTarihi.Year < 2000)
            {
                if (user.OlusturmaTarihi.HasValue)
                {
                    model.BaslangicTarihi = user.OlusturmaTarihi.Value;
                }
                else
                {
                    model.BaslangicTarihi = DateTime.Now;
                }
            }

            // Validasyon
            if (model.BitisTarihi != default(DateTime) && model.BitisTarihi.Year >= 2000 && 
                model.BitisTarihi < model.BaslangicTarihi)
            {
                ModelState.AddModelError("BitisTarihi", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            }

            if (!ModelState.IsValid)
            {
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .OrderBy(b => b.Ad)
                    .ToListAsync();

                ViewBag.Birimler = birimler;
                return View(model);
            }

            // Güncelleme - sadece doldurulmuş alanları güncelle
            stajyer.BaslangicTarihi = model.BaslangicTarihi;
            
            // Bitiş tarihi sadece doldurulmuşsa güncelle
            if (model.BitisTarihi != default(DateTime) && model.BitisTarihi.Year >= 2000)
            {
                stajyer.BitisTarihi = model.BitisTarihi;
            }
            
            // Birim sadece seçilmişse güncelle
            if (model.BirimId > 0)
            {
                stajyer.BirimId = model.BirimId;
            }
            
            stajyer.Okul = model.Okul;
            stajyer.Bolum = model.Bolum;
            stajyer.Sinif = model.Sinif;

            // Toplam gün hesaplama (sadece bitiş tarihi varsa)
            if (stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000)
            {
                var toplamGun = (stajyer.BitisTarihi - stajyer.BaslangicTarihi).Days + 1;
                stajyer.ToplamGun = toplamGun;

                // Devam oranı güncelle
                var tamamlananGun = (DateTime.Now - stajyer.BaslangicTarihi).Days + 1;
                if (tamamlananGun > toplamGun) tamamlananGun = toplamGun;
                if (tamamlananGun < 0) tamamlananGun = 0;
                stajyer.TamamlananGun = tamamlananGun;
                stajyer.DevamOrani = toplamGun > 0 ? (decimal)(tamamlananGun * 100.0 / toplamGun) : 0;
            }
            else
            {
                stajyer.ToplamGun = 0;
                stajyer.TamamlananGun = 0;
                stajyer.DevamOrani = 0;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Bilgileriniz başarıyla güncellendi.";
            return RedirectToAction("Bilgilerim");
        }
    }
}
