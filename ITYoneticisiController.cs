using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using stajyer_takip.Models;

namespace stajyer_takip.Controllers
{
    [Authorize(Roles = "ITYoneticisi")]
    public class ITYoneticisiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ITYoneticisiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var son1Yil = DateTime.Now.AddYears(-1);
            // Toplam stajyer: son 1 yılda staj başlatan herkes (staj süresi bitenler dahil)
            var toplamStajyer = await _context.Stajyerler.CountAsync(s =>
                (s.BaslangicTarihi != default && s.BaslangicTarihi >= son1Yil) ||
                (s.BaslangicTarihi == default && s.OlusturmaTarihi >= son1Yil));
            var aktifStajyer = await _context.Stajyerler.CountAsync(s => s.Aktif && s.BitisTarihi >= DateTime.Now);
            var toplamGorev = await _context.GunlukGorevler.CountAsync();
            var onayBekleyenGorev = await _context.GunlukGorevler.CountAsync(g => !g.Onaylandi);
            var toplamBirim = await _context.Birimler.CountAsync(b => b.Aktif);

            // Teknoloji kullanım istatistikleri
            var teknolojiKullanim = await _context.GunlukGorevler
                .Where(g => !string.IsNullOrEmpty(g.KullanilanTeknoloji))
                .GroupBy(g => g.KullanilanTeknoloji)
                .Select(g => new { Teknoloji = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(10)
                .ToListAsync();

            ViewBag.ToplamStajyer = toplamStajyer;
            ViewBag.AktifStajyer = aktifStajyer;
            ViewBag.ToplamGorev = toplamGorev;
            ViewBag.OnayBekleyenGorev = onayBekleyenGorev;
            ViewBag.ToplamBirim = toplamBirim;
            ViewBag.TeknolojiKullanim = teknolojiKullanim;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> KullaniciYonetimi()
        {
            var kullanicilar = await _userManager.Users
                .ToListAsync();

            var kullaniciRolleri = new Dictionary<string, List<string>>();
            foreach (var kullanici in kullanicilar)
            {
                var roller = await _userManager.GetRolesAsync(kullanici);
                kullaniciRolleri[kullanici.Id] = roller.ToList();
            }

            ViewBag.KullaniciRolleri = kullaniciRolleri;

            return View(kullanicilar);
        }

        [HttpGet]
        public async Task<IActionResult> RolAta(string userId)
        {
            var kullanici = await _userManager.FindByIdAsync(userId);
            if (kullanici == null) return NotFound();

            var mevcutRoller = await _userManager.GetRolesAsync(kullanici);
            ViewBag.MevcutRoller = mevcutRoller;

            return View(kullanici);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RolAta(string userId, string[] roller, string manuelGorev)
        {
            var kullanici = await _userManager.FindByIdAsync(userId);
            if (kullanici == null) return NotFound();

            // Rol checkbox'ları kaldırıldı; sadece manuel görev alanı kullanılıyor. Rolleri değiştirme.
            if (roller != null && roller.Length > 0)
            {
                var mevcutRoller = await _userManager.GetRolesAsync(kullanici);
                await _userManager.RemoveFromRolesAsync(kullanici, mevcutRoller);
                await _userManager.AddToRolesAsync(kullanici, roller);
            }

            if (!string.IsNullOrWhiteSpace(manuelGorev))
            {
                var manuelGorevKayit = new Entity.ManuelGorev
                {
                    UserId = userId,
                    Aciklama = manuelGorev.Trim(),
                    OlusturmaTarihi = DateTime.Now,
                    AtayanUserId = _userManager.GetUserId(User)
                };
                _context.ManuelGorevler.Add(manuelGorevKayit);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "İşlem başarıyla kaydedildi.";
            return RedirectToAction("KullaniciYonetimi");
        }

        [HttpGet]
        public async Task<IActionResult> StajyerListesi(bool? aktifOnly)
        {
            var son1Yil = DateTime.Now.AddYears(-1);
            var query = _context.Stajyerler
                .Include(s => s.User)
                .Include(s => s.Birim)
                .AsQueryable();

            if (aktifOnly == true)
            {
                query = query.Where(s => s.Aktif && s.BitisTarihi >= DateTime.Now);
            }
            else
            {
                query = query.Where(s =>
                    (s.BaslangicTarihi != default(DateTime) && s.BaslangicTarihi >= son1Yil) ||
                    (s.BaslangicTarihi == default(DateTime) && s.OlusturmaTarihi >= son1Yil));
            }

            var list = await query.OrderByDescending(s => s.BaslangicTarihi).ToListAsync();
            ViewBag.AktifOnly = aktifOnly == true;
            ViewBag.Baslik = aktifOnly == true ? "Aktif Stajyerler" : "Son 1 Yılda Staj Yapan Tüm Stajyerler";
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> GorevListesi(bool? onayBekleyen)
        {
            var query = _context.GunlukGorevler
                .Include(g => g.Stajyer)
                    .ThenInclude(s => s!.User)
                .Include(g => g.Stajyer)
                    .ThenInclude(s => s!.Birim)
                .AsQueryable();

            if (onayBekleyen == true)
                query = query.Where(g => !g.Onaylandi);

            var list = await query.OrderByDescending(g => g.Tarih).ThenByDescending(g => g.Id).ToListAsync();
            ViewBag.OnayBekleyen = onayBekleyen == true;
            ViewBag.Baslik = onayBekleyen == true ? "Onay Bekleyen Görevler" : "Tüm Görevler";
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> BirimOzeti()
        {
            var birimler = await _context.Birimler
                .Include(b => b.Stajyerler)
                .Where(b => b.Aktif)
                .OrderBy(b => b.Ad)
                .ToListAsync();
            var liste = birimler.Select(b => new BirimOzetiItem { Ad = b.Ad ?? "", StajyerSayisi = b.Stajyerler.Count(s => s.Aktif) }).ToList();
            ViewBag.ToplamBirim = liste.Count;
            ViewBag.ToplamStajyer = liste.Sum(x => x.StajyerSayisi);
            return View(liste);
        }
    }
}
