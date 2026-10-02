using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Controllers
{
    [Authorize(Roles = "IK")]
    public class IKController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IKController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            // Tüm stajyerler
            var stajyerler = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .Where(s => s.Aktif)
                .ToListAsync();

            // Tüm birimler ve kontenjan durumu
            var birimler = await _context.Birimler
                .Include(b => b.Stajyerler)
                .Where(b => b.Aktif)
                .ToListAsync();

            // Her birim için gerçek stajyer sayısını hesapla ve güncelle
            foreach (var birim in birimler)
            {
                var gercekStajyerSayisi = await _context.Stajyerler
                    .CountAsync(s => s.BirimId == birim.Id && s.Aktif);
                
                // Veritabanındaki değeri güncelle
                birim.MevcutStajyerSayisi = gercekStajyerSayisi;
            }
            
            // Değişiklikleri kaydet
            await _context.SaveChangesAsync();

            // İstatistikler
            var toplamStajyer = stajyerler.Count;
            var aktifStajyer = stajyerler.Count(s => s.BitisTarihi != default(DateTime) && s.BitisTarihi.Year >= 2000 && s.BitisTarihi >= DateTime.Now);
            var bitmisStajyer = stajyerler.Count(s => s.BitisTarihi != default(DateTime) && s.BitisTarihi.Year >= 2000 && s.BitisTarihi < DateTime.Now);

            var user = await _userManager.GetUserAsync(User);
            var manuelGorevler = user != null
                ? await _context.ManuelGorevler
                    .Where(m => m.UserId == user.Id)
                    .OrderByDescending(m => m.OlusturmaTarihi)
                    .Take(10)
                    .ToListAsync()
                : new List<ManuelGorev>();

            ViewBag.ToplamStajyer = toplamStajyer;
            ViewBag.AktifStajyer = aktifStajyer;
            ViewBag.BitmisStajyer = bitmisStajyer;
            ViewBag.Birimler = birimler;
            ViewBag.ManuelGorevler = manuelGorevler;

            return View(stajyerler);
        }

        [HttpGet]
        public async Task<IActionResult> StajyerEkle()
        {
            var birimler = await _context.Birimler
                .Where(b => b.Aktif)
                .ToListAsync();

            ViewBag.Birimler = birimler;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StajyerEkle(Stajyer stajyer, string KullaniciAdi, string KullaniciSoyadi, string KullaniciEmail)
        {
            // Validasyon
            if (string.IsNullOrEmpty(KullaniciAdi) || string.IsNullOrEmpty(KullaniciSoyadi) || string.IsNullOrEmpty(KullaniciEmail))
            {
                ModelState.AddModelError("", "Kullanıcı adı, soyadı ve email gereklidir.");
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .ToListAsync();
                ViewBag.Birimler = birimler;
                return View(stajyer);
            }

            // Email kontrolü
            var mevcutKullanici = await _userManager.FindByEmailAsync(KullaniciEmail);
            if (mevcutKullanici != null)
            {
                ModelState.AddModelError("", "Bu email adresi zaten kullanılıyor.");
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .ToListAsync();
                ViewBag.Birimler = birimler;
                return View(stajyer);
            }

            // Kontenjan kontrolü
            var birim = await _context.Birimler.FindAsync(stajyer.BirimId);
            if (birim == null)
            {
                ModelState.AddModelError("", "Birim bulunamadı.");
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .ToListAsync();
                ViewBag.Birimler = birimler;
                return View(stajyer);
            }

            if (birim.MevcutStajyerSayisi >= birim.Kontenjan)
            {
                ModelState.AddModelError("", "Bu birim için kontenjan dolu.");
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .ToListAsync();
                ViewBag.Birimler = birimler;
                return View(stajyer);
            }

            // Yeni kullanıcı oluştur
            var yeniKullanici = new ApplicationUser
            {
                UserName = KullaniciEmail,
                Email = KullaniciEmail,
                Ad = KullaniciAdi,
                Soyad = KullaniciSoyadi,
                Aktif = true,
                OlusturmaTarihi = DateTime.Now
            };

            // Varsayılan şifre: Email'in @ öncesi kısmı + "123"
            var varsayilanSifre = KullaniciEmail.Split('@')[0] + "123";

            var kullaniciSonuc = await _userManager.CreateAsync(yeniKullanici, varsayilanSifre);
            if (!kullaniciSonuc.Succeeded)
            {
                foreach (var error in kullaniciSonuc.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                var birimler = await _context.Birimler
                    .Where(b => b.Aktif)
                    .ToListAsync();
                ViewBag.Birimler = birimler;
                return View(stajyer);
            }

            // Kullanıcıyı Stajyer rolüne ekle
            if (await _roleManager.RoleExistsAsync("Stajyer"))
            {
                await _userManager.AddToRoleAsync(yeniKullanici, "Stajyer");
            }

            // Stajyer kaydı oluştur
            stajyer.UserId = yeniKullanici.Id;
            
            // Toplam gün hesaplama (hafta sonları hariç)
            var toplamIsGunu = 0;
            if (stajyer.BaslangicTarihi != default(DateTime) && stajyer.BitisTarihi != default(DateTime))
            {
                var tarih = stajyer.BaslangicTarihi;
                while (tarih <= stajyer.BitisTarihi)
                {
                    if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                    {
                        toplamIsGunu++;
                    }
                    tarih = tarih.AddDays(1);
                }
            }
            
            stajyer.ToplamGun = toplamIsGunu;
            stajyer.TamamlananGun = 0;
            stajyer.DevamOrani = 0;
            stajyer.Aktif = true;
            stajyer.OlusturmaTarihi = DateTime.Now;

            _context.Stajyerler.Add(stajyer);
            birim.MevcutStajyerSayisi++;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Stajyer başarıyla eklendi. Varsayılan şifre: {varsayilanSifre}";
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "ITYoneticisi,IK")]
        public async Task<IActionResult> BirimYonetimi()
        {
            var birimler = await _context.Birimler
                .Include(b => b.Stajyerler)
                .Include(b => b.Sorumlular)
                    .ThenInclude(bs => bs.User)
                .Where(b => b.Aktif)
                .ToListAsync();

            return View(birimler);
        }

        [HttpGet]
        public IActionResult BirimEkle()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BirimEkle(Birim birim)
        {
            birim.Aktif = true;
            birim.MevcutStajyerSayisi = 0;
            birim.OlusturmaTarihi = DateTime.Now;

            _context.Birimler.Add(birim);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Birim başarıyla eklendi.";
            return RedirectToAction("BirimYonetimi");
        }

        [HttpGet]
        public async Task<IActionResult> BirimDuzenle(int id)
        {
            var birim = await _context.Birimler.FindAsync(id);
            if (birim == null || !birim.Aktif)
            {
                return NotFound();
            }

            return View(birim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BirimDuzenle(int id, Birim birim)
        {
            if (id != birim.Id)
            {
                return NotFound();
            }

            var mevcutBirim = await _context.Birimler.FindAsync(id);
            if (mevcutBirim == null || !mevcutBirim.Aktif)
            {
                return NotFound();
            }

            // Kontenjan kontrolü - Mevcut stajyer sayısından az olamaz
            if (birim.Kontenjan < mevcutBirim.MevcutStajyerSayisi)
            {
                ModelState.AddModelError("Kontenjan", $"Kontenjan, mevcut stajyer sayısından ({mevcutBirim.MevcutStajyerSayisi}) az olamaz.");
                return View(birim);
            }

            
            mevcutBirim.Ad = birim.Ad;
            mevcutBirim.Aciklama = birim.Aciklama;
            mevcutBirim.Kontenjan = birim.Kontenjan;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Birim başarıyla güncellendi.";
            return RedirectToAction("BirimYonetimi");
        }

        [HttpGet]
        public async Task<IActionResult> StajyerDetay(int id)
        {
            var stajyer = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .Include(s => s.GunlukGorevler)
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy.Yetenek)
                .Include(s => s.PerformansSkorlari)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (stajyer == null) return NotFound();

            // Kalan iş günü hesaplama (hafta sonları hariç)
            int kalanGun = 0;
            if (stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000)
            {
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
            }

            ViewBag.KalanGun = kalanGun;

            return View(stajyer);
        }

        [HttpGet]
        public async Task<IActionResult> GenelYetenekHaritasi()
        {
            // Tüm stajyerler ve yetenekleri
            var stajyerler = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy.Yetenek)
                .Where(s => s.Aktif)
                .ToListAsync();

            var tumYetenekler = await _context.Yetenekler
                .Where(y => y.Aktif)
                .ToListAsync();

            ViewBag.TumYetenekler = tumYetenekler;

            return View(stajyerler);
        }
    }
}
