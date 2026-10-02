using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Controllers
{
    [Authorize(Roles = "BirimSorumlusu")]
    public class BirimSorumlusuController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BirimSorumlusuController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? arama)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .Include(bs => bs.Birim)
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            
            if (birimSorumlusu == null)
            {
                
                var varsayilanBirim = await _context.Birimler
                    .Where(b => b.Aktif)
                    .FirstOrDefaultAsync();

                if (varsayilanBirim == null)
                {
                    return View("SorumluKayitYok");
                }

                birimSorumlusu = new BirimSorumlusu
                {
                    UserId = user.Id,
                    BirimId = varsayilanBirim.Id,
                    AtamaTarihi = DateTime.Now,
                    Aktif = true
                };

                _context.BirimSorumlulari.Add(birimSorumlusu);
                await _context.SaveChangesAsync();

                
                birimSorumlusu = await _context.BirimSorumlulari
                    .Include(bs => bs.Birim)
                    .FirstOrDefaultAsync(bs => bs.UserId == user.Id);
            }

            if (birimSorumlusu == null)
                return NotFound();

            
            var stajyerler = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .Where(s => s.BirimId == birimSorumlusu.BirimId && s.Aktif)
                .ToListAsync();

            
            var stajyerKalanGunler = new Dictionary<int, int>();
            foreach (var stajyer in stajyerler)
            {
                if (stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000)
                {
                    var bugun = DateTime.Now.Date;
                    if (bugun < stajyer.BitisTarihi)
                    {
                        var kalanGun = 0;
                        var kalanTarih = bugun.AddDays(1);
                        while (kalanTarih <= stajyer.BitisTarihi)
                        {
                            if (kalanTarih.DayOfWeek != DayOfWeek.Saturday && kalanTarih.DayOfWeek != DayOfWeek.Sunday)
                            {
                                kalanGun++;
                            }
                            kalanTarih = kalanTarih.AddDays(1);
                        }
                        stajyerKalanGunler[stajyer.Id] = kalanGun;
                    }
                    else
                    {
                        stajyerKalanGunler[stajyer.Id] = 0;
                    }
                }
                else
                {
                    stajyerKalanGunler[stajyer.Id] = 0;
                }
            }

            
            var onayBekleyenGorevler = await _context.GunlukGorevler
                .Include(g => g.Stajyer)
                    .ThenInclude(s => s!.User)
                .Where(g => g.Stajyer != null && g.Stajyer.BirimId == birimSorumlusu.BirimId && !g.Onaylandi)
                .OrderByDescending(g => g.Tarih)
                .ToListAsync();

            var manuelGorevler = await _context.ManuelGorevler
                .Where(m => m.UserId == user.Id)
                .OrderByDescending(m => m.OlusturmaTarihi)
                .Take(10)
                .ToListAsync();

            ViewBag.Stajyerler = stajyerler;
            ViewBag.StajyerKalanGunler = stajyerKalanGunler;
            ViewBag.OnayBekleyenGorevler = onayBekleyenGorevler;
            ViewBag.Birim = birimSorumlusu.Birim;
            ViewBag.StajyerArama = arama?.Trim() ?? "";
            ViewBag.ManuelGorevler = manuelGorevler;

            return View(birimSorumlusu);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StajyereGorevAta(int stajyerId, string aciklama, bool returnToDetay = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);
            if (birimSorumlusu == null)
            {
                TempData["Hata"] = "Birim sorumlusu kaydı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var stajyer = await _context.Stajyerler
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == stajyerId && s.BirimId == birimSorumlusu.BirimId && s.Aktif);
            if (stajyer == null)
            {
                if (returnToDetay) TempData["GorevHata"] = "Stajyer bulunamadı veya bu birime ait değil.";
                else TempData["Hata"] = "Stajyer bulunamadı veya bu birime ait değil.";
                return returnToDetay ? RedirectToAction(nameof(StajyerDetay), new { id = stajyerId }) : RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(aciklama))
            {
                if (returnToDetay) TempData["GorevHata"] = "Görev açıklaması boş olamaz.";
                else TempData["Hata"] = "Görev açıklaması boş olamaz.";
                return returnToDetay ? RedirectToAction(nameof(StajyerDetay), new { id = stajyerId }) : RedirectToAction(nameof(Index));
            }

            var gorev = new ManuelGorev
            {
                UserId = stajyer.UserId,
                Aciklama = aciklama.Trim(),
                OlusturmaTarihi = DateTime.Now,
                AtayanUserId = user.Id
            };
            _context.ManuelGorevler.Add(gorev);
            await _context.SaveChangesAsync();

            if (returnToDetay)
            {
                TempData["GorevMesaj"] = "Görev başarıyla atandı. Stajyer anasayfasında görünecektir.";
                return RedirectToAction(nameof(StajyerDetay), new { id = stajyerId });
            }
            TempData["Success"] = "Görev başarıyla atandı. Stajyer anasayfasında görünecektir.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GorevOnayla(int id)
        {
            var gorev = await _context.GunlukGorevler
                .Include(g => g.Stajyer)
                    .ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gorev == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            if (birimSorumlusu == null || gorev.Stajyer == null || gorev.Stajyer.BirimId != birimSorumlusu.BirimId)
            {
                return Forbid();
            }

            return View(gorev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GorevOnayla(int id, int? puan, string? yorum)
        {
            var gorev = await _context.GunlukGorevler
                .Include(g => g.Stajyer)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gorev == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            if (birimSorumlusu == null || gorev.Stajyer == null || gorev.Stajyer.BirimId != birimSorumlusu.BirimId)
            {
                return Forbid();
            }

            gorev.Onaylandi = true;
            gorev.OnayTarihi = DateTime.Now;
            gorev.OnaylayanUserId = user.Id;
            gorev.Puan = puan;
            gorev.Yorum = yorum;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Görev başarıyla onaylandı.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> StajyerDetay(int id)
        {
            var stajyer = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .Include(s => s.GunlukGorevler)
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy!.Yetenek)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (stajyer == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            if (birimSorumlusu == null || stajyer.BirimId != birimSorumlusu.BirimId)
            {
                return Forbid();
            }

            
            PerformansSkoru? sonPerformans = null;
            var stajBitti = stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000 && DateTime.Now >= stajyer.BitisTarihi;
            if (stajBitti)
            {
                sonPerformans = await _context.PerformansSkorlari
                    .Where(p => p.StajyerId == stajyer.Id && p.SorumluFinalPuanı.HasValue)
                    .OrderByDescending(p => p.HesaplamaTarihi)
                    .FirstOrDefaultAsync();
            }

            ViewBag.SonPerformans = sonPerformans;
            ViewBag.StajBitti = stajBitti;

            return View(stajyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PerformansPuanVer(int stajyerId, decimal puan)
        {
            if (puan < 0 || puan > 100)
            {
                TempData["Error"] = "Puan 0-100 arasında olmalıdır.";
                return RedirectToAction("StajyerDetay", new { id = stajyerId });
            }

            var stajyer = await _context.Stajyerler
                .FirstOrDefaultAsync(s => s.Id == stajyerId);

            if (stajyer == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            if (birimSorumlusu == null || stajyer.BirimId != birimSorumlusu.BirimId)
            {
                return Forbid();
            }

            
            var stajBitti = stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000 && DateTime.Now >= stajyer.BitisTarihi;
            if (!stajBitti)
            {
                TempData["Error"] = "Staj henüz bitmemiş. Puan vermek için stajın bitmesi gerekiyor.";
                return RedirectToAction("StajyerDetay", new { id = stajyerId });
            }

            
            var performansSkoru = await _context.PerformansSkorlari
                .Where(p => p.StajyerId == stajyerId)
                .OrderByDescending(p => p.HesaplamaTarihi)
                .FirstOrDefaultAsync();

            if (performansSkoru == null)
            {
                
                performansSkoru = new PerformansSkoru
                {
                    StajyerId = stajyerId,
                    BaslangicTarihi = stajyer.BaslangicTarihi,
                    BitisTarihi = stajyer.BitisTarihi,
                    GunlukGirisDuzeni = 0,
                    GorevTamamlamaOrani = 0,
                    SorumluDegerlendirmesi = 0,
                    GecTeslimOrani = 0,
                    ToplamSkor = 0,
                    SorumluFinalPuanı = puan
                };
                _context.PerformansSkorlari.Add(performansSkoru);
            }
            else
            {
                
                performansSkoru.SorumluFinalPuanı = puan;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Performans puanı başarıyla kaydedildi: {puan}";
            return RedirectToAction("StajyerDetay", new { id = stajyerId });
        }

        [HttpGet]
        public async Task<IActionResult> BirimYetenekHaritasi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var birimSorumlusu = await _context.BirimSorumlulari
                .Include(bs => bs.Birim)
                .FirstOrDefaultAsync(bs => bs.UserId == user.Id);

            if (birimSorumlusu == null) return NotFound();

            
            var stajyerler = await _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Yetenekler)
                    .ThenInclude(sy => sy!.Yetenek)
                .Where(s => s.BirimId == birimSorumlusu.BirimId && s.Aktif)
                .ToListAsync();

            var tumYetenekler = await _context.Yetenekler
                .Where(y => y.Aktif)
                .ToListAsync();

            ViewBag.TumYetenekler = tumYetenekler;
            ViewBag.Birim = birimSorumlusu.Birim;

            return View(stajyerler);
        }
    }
}
