using System.Security.Claims;
using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using stajyer_takip.Services;

namespace stajyer_takip.Controllers
{
    public class EtkinliklerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEtkinlikMailService _etkinlikMail;

        public EtkinliklerController(ApplicationDbContext context, IWebHostEnvironment env, IEtkinlikMailService etkinlikMail)
        {
            _context = context;
            _env = env;
            _etkinlikMail = etkinlikMail;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var etkinlikler = await _context.Etkinlikler
                .Where(e => e.Aktif)
                .OrderBy(e => e.Tarih)
                .ToListAsync();
            return View(etkinlikler);
        }

        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var etkinlik = await _context.Etkinlikler
                .Include(e => e.Kayitlar)
                    .ThenInclude(k => k.User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (etkinlik == null)
                return NotFound();
            if (!etkinlik.Aktif && !User.IsInRole("ITYoneticisi") && !User.IsInRole("BirimSorumlusu"))
                return NotFound();

            if (User.IsInRole("BirimSorumlusu"))
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var birimSorumlusu = await _context.BirimSorumlulari
                    .FirstOrDefaultAsync(bs => bs.UserId == currentUserId);
                if (birimSorumlusu != null)
                {
                    var birimdenStajyerUserIds = await _context.Stajyerler
                        .Where(s => s.BirimId == birimSorumlusu.BirimId)
                        .Select(s => s.UserId)
                        .ToListAsync();
                    var birimdenKayitlilar = etkinlik.Kayitlar?
                        .Where(k => k.UserId != null && birimdenStajyerUserIds.Contains(k.UserId))
                        .ToList() ?? new List<EtkinlikKayit>();
                    ViewBag.BirimdenKayitlilar = birimdenKayitlilar;
                }
                else
                {
                    ViewBag.BirimdenKayitlilar = new List<EtkinlikKayit>();
                }
            }

            return View(etkinlik);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kayit(int etkinlikId)
        {
            // Etkinliğe kayıt için giriş zorunlu
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Detay", "Etkinlikler", new { id = etkinlikId }) });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["EtkinlikMesaj"] = "Oturum bilginiz alınamadı. Lütfen tekrar giriş yapın.";
                return RedirectToAction(nameof(Detay), new { id = etkinlikId });
            }

            var etkinlik = await _context.Etkinlikler
                .Include(e => e.Kayitlar)
                .FirstOrDefaultAsync(e => e.Id == etkinlikId && e.Aktif);
            if (etkinlik == null)
            {
                TempData["EtkinlikMesaj"] = "Etkinlik bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var etkinlikBitis = etkinlik.BitisTarihi ?? etkinlik.Tarih;
            if (etkinlikBitis < DateTime.Now)
            {
                TempData["EtkinlikMesaj"] = "Bu etkinliğin tarihi geçmiştir. Kayıt yapılamaz.";
                return RedirectToAction(nameof(Detay), new { id = etkinlikId });
            }

            if (etkinlik.Kontenjan.HasValue && etkinlik.Kayitlar.Count >= etkinlik.Kontenjan.Value)
            {
                TempData["EtkinlikMesaj"] = "Bu etkinliğin kontenjanı dolmuştur.";
                return RedirectToAction(nameof(Detay), new { id = etkinlikId });
            }

            var zatenKayitli = await _context.EtkinlikKayitlari
                .AnyAsync(k => k.EtkinlikId == etkinlikId && k.UserId == userId);
            if (zatenKayitli)
            {
                TempData["EtkinlikMesaj"] = "Bu etkinliğe zaten kayıtlısınız.";
                return RedirectToAction(nameof(Detay), new { id = etkinlikId });
            }

            var user = await _context.Users.FindAsync(userId);
            var ad = user != null ? $"{user.Ad} {user.Soyad}".Trim() : null;
            var email = user?.Email;

            var kayit = new EtkinlikKayit
            {
                EtkinlikId = etkinlikId,
                UserId = userId,
                KatilimciAdi = ad,
                KatilimciEmail = email
            };
            _context.EtkinlikKayitlari.Add(kayit);
            await _context.SaveChangesAsync();

            TempData["EtkinlikMesaj"] = "Etkinliğe başarıyla kaydoldunuz.";
            return RedirectToAction(nameof(Detay), new { id = etkinlikId });
        }

        [HttpGet]
        [Authorize(Roles = "ITYoneticisi,BirimSorumlusu")]
        public IActionResult Ekle()
        {
            return View(new Etkinlik { Tarih = DateTime.Now, BitisTarihi = DateTime.Now.AddHours(2), Aktif = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ITYoneticisi,BirimSorumlusu")]
        public async Task<IActionResult> Ekle(Etkinlik model, IFormFile? etkinlikResmi)
        {
            if (string.IsNullOrWhiteSpace(model.Ad))
            {
                ModelState.AddModelError("Ad", "Etkinlik başlığı zorunludur.");
            }
            if (model.Ad != null && model.Ad.Length > 255)
            {
                ModelState.AddModelError("Ad", "Başlık en fazla 255 karakter olabilir.");
            }
            if (model.KisaAciklama != null && model.KisaAciklama.Length > 512)
            {
                ModelState.AddModelError("KisaAciklama", "Kısa açıklama en fazla 512 karakter olabilir.");
            }
            if (model.BitisTarihi.HasValue && model.BitisTarihi < model.Tarih)
            {
                ModelState.AddModelError("BitisTarihi", "Bitiş tarihi başlangıçtan sonra olmalıdır.");
            }

            const int maxFileSizeBytes = 2 * 1024 * 1024; // 2MB
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            if (etkinlikResmi != null && etkinlikResmi.Length > 0)
            {
                if (etkinlikResmi.Length > maxFileSizeBytes)
                    ModelState.AddModelError("etkinlikResmi", "Dosya boyutu en fazla 2MB olabilir.");
                var ext = Path.GetExtension(etkinlikResmi.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    ModelState.AddModelError("etkinlikResmi", "Sadece JPG, JPEG veya PNG yükleyebilirsiniz.");
            }

            if (!ModelState.IsValid)
                return View(model);

            if (etkinlikResmi != null && etkinlikResmi.Length > 0)
            {
                var imagesPath = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(imagesPath))
                    Directory.CreateDirectory(imagesPath);
                var fileName = $"etkinlik_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(etkinlikResmi.FileName)}";
                var filePath = Path.Combine(imagesPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await etkinlikResmi.CopyToAsync(stream);
                model.GorselDosyaAdi = fileName;
            }

            model.Saat = model.Tarih.ToString("HH:mm");
            model.OlusturmaTarihi = DateTime.Now;
            model.Aktif = Request.Form.TryGetValue("Aktif", out var aktifs) && aktifs.Contains("true");
            _context.Etkinlikler.Add(model);
            await _context.SaveChangesAsync();

            TempData["EtkinlikMesaj"] = "Etkinlik başarıyla oluşturuldu. Aşağıdaki listede görüntülenir.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "ITYoneticisi,BirimSorumlusu")]
        public async Task<IActionResult> Duzenle(int id)
        {
            var etkinlik = await _context.Etkinlikler.FindAsync(id);
            if (etkinlik == null)
                return NotFound();
            return View(etkinlik);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ITYoneticisi,BirimSorumlusu")]
        public async Task<IActionResult> Duzenle(Etkinlik model, IFormFile? etkinlikResmi)
        {
            if (model.Id == 0)
                return NotFound();
            if (string.IsNullOrWhiteSpace(model.Ad))
                ModelState.AddModelError("Ad", "Etkinlik başlığı zorunludur.");
            if (model.Ad != null && model.Ad.Length > 255)
                ModelState.AddModelError("Ad", "Başlık en fazla 255 karakter olabilir.");
            if (model.KisaAciklama != null && model.KisaAciklama.Length > 512)
                ModelState.AddModelError("KisaAciklama", "Kısa açıklama en fazla 512 karakter olabilir.");
            if (model.BitisTarihi.HasValue && model.BitisTarihi < model.Tarih)
                ModelState.AddModelError("BitisTarihi", "Bitiş tarihi başlangıçtan sonra olmalıdır.");

            const int maxFileSizeBytes = 2 * 1024 * 1024;
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            if (etkinlikResmi != null && etkinlikResmi.Length > 0)
            {
                if (etkinlikResmi.Length > maxFileSizeBytes)
                    ModelState.AddModelError("etkinlikResmi", "Dosya boyutu en fazla 2MB olabilir.");
                var ext = Path.GetExtension(etkinlikResmi.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    ModelState.AddModelError("etkinlikResmi", "Sadece JPG, JPEG veya PNG yükleyebilirsiniz.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var mevcut = await _context.Etkinlikler.FindAsync(model.Id);
            if (mevcut == null)
                return NotFound();

            mevcut.Ad = model.Ad?.Trim();
            mevcut.Tarih = model.Tarih;
            mevcut.BitisTarihi = model.BitisTarihi;
            mevcut.Saat = model.Tarih.ToString("HH:mm");
            mevcut.KisaAciklama = model.KisaAciklama;
            mevcut.Aciklama = model.Aciklama;
            mevcut.Yer = model.Yer;
            mevcut.Kontenjan = model.Kontenjan;
            mevcut.Aktif = Request.Form.TryGetValue("Aktif", out var aktifs) && aktifs.Contains("true");

            if (etkinlikResmi != null && etkinlikResmi.Length > 0)
            {
                var imagesPath = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(imagesPath))
                    Directory.CreateDirectory(imagesPath);
                var fileName = $"etkinlik_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(etkinlikResmi.FileName)}";
                var filePath = Path.Combine(imagesPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await etkinlikResmi.CopyToAsync(stream);
                mevcut.GorselDosyaAdi = fileName;
            }

            await _context.SaveChangesAsync();

            var mailGonderilenSayisi = 0;
            if (User.IsInRole("ITYoneticisi"))
            {
                mailGonderilenSayisi = await _etkinlikMail.GuncellemeBildirimiTumKatilimcilaraGonderAsync(mevcut);
            }
            else if (User.IsInRole("BirimSorumlusu"))
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var birimSorumlusu = await _context.BirimSorumlulari
                    .FirstOrDefaultAsync(bs => bs.UserId == currentUserId && bs.Aktif);
                if (birimSorumlusu != null)
                    mailGonderilenSayisi = await _etkinlikMail.GuncellemeBildirimiGonderAsync(mevcut, birimSorumlusu.BirimId);
            }

            TempData["EtkinlikMesaj"] = "Etkinlik başarıyla güncellendi."
                + (mailGonderilenSayisi > 0 ? $" {mailGonderilenSayisi} kişiye bilgilendirme maili gönderildi." : "");
            return RedirectToAction(nameof(Detay), new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ITYoneticisi,BirimSorumlusu")]
        public async Task<IActionResult> Sil(int id)
        {
            var etkinlik = await _context.Etkinlikler
                .Include(e => e.Kayitlar!)
                    .ThenInclude(k => k.User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (etkinlik == null)
                return NotFound();

            var silindiMailSayisi = 0;
            if (User.IsInRole("ITYoneticisi") && etkinlik.Kayitlar?.Any() == true)
            {
                var aliciEpostalari = etkinlik.Kayitlar!
                    .Select(k => (k.KatilimciEmail ?? k.User?.Email)?.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (aliciEpostalari.Count > 0)
                {
                    var etkinlikAdi = etkinlik.Ad;
                    _context.EtkinlikKayitlari.RemoveRange(etkinlik.Kayitlar!);
                    _context.Etkinlikler.Remove(etkinlik);
                    await _context.SaveChangesAsync();

                    silindiMailSayisi = await _etkinlikMail.SilindiBildirimiGonderAsync(etkinlikAdi, aliciEpostalari);
                    TempData["EtkinlikMesaj"] = "Etkinlik silindi." + (silindiMailSayisi > 0 ? $" {silindiMailSayisi} kişiye bilgilendirme maili gönderildi." : "");
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (User.IsInRole("BirimSorumlusu") && etkinlik.Kayitlar?.Any() == true)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var birimSorumlusu = await _context.BirimSorumlulari
                    .FirstOrDefaultAsync(bs => bs.UserId == currentUserId && bs.Aktif);
                if (birimSorumlusu != null)
                {
                    var birimdenStajyerUserIds = await _context.Stajyerler
                        .Where(s => s.BirimId == birimSorumlusu.BirimId)
                        .Select(s => s.UserId)
                        .ToListAsync();
                    var aliciEpostalari = etkinlik.Kayitlar!
                        .Where(k => k.UserId != null && birimdenStajyerUserIds.Contains(k.UserId))
                        .Select(k => (k.KatilimciEmail ?? k.User?.Email)?.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (aliciEpostalari.Count > 0)
                    {
                        var etkinlikAdi = etkinlik.Ad;
                        _context.EtkinlikKayitlari.RemoveRange(etkinlik.Kayitlar!);
                        _context.Etkinlikler.Remove(etkinlik);
                        await _context.SaveChangesAsync();

                        silindiMailSayisi = await _etkinlikMail.SilindiBildirimiGonderAsync(etkinlikAdi, aliciEpostalari);
                        TempData["EtkinlikMesaj"] = "Etkinlik silindi." + (silindiMailSayisi > 0 ? $" {silindiMailSayisi} kişiye bilgilendirme maili gönderildi." : "");
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            if (etkinlik.Kayitlar?.Any() == true)
                _context.EtkinlikKayitlari.RemoveRange(etkinlik.Kayitlar);
            _context.Etkinlikler.Remove(etkinlik);
            await _context.SaveChangesAsync();

            TempData["EtkinlikMesaj"] = "Etkinlik silindi." + (silindiMailSayisi > 0 ? $" {silindiMailSayisi} kişiye bilgilendirme maili gönderildi." : "");
            return RedirectToAction(nameof(Index));
        }
    }
}
