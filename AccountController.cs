using DataAccessLayer;
using Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email ve şifre gereklidir.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.Aktif)
            {
                ModelState.AddModelError("", "Geçersiz email veya şifre.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (returnUrl != null && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (roles.Contains("Stajyer"))
                    return RedirectToAction("Index", "Stajyer");
                else if (roles.Contains("BirimSorumlusu"))
                    return RedirectToAction("Index", "BirimSorumlusu");
                else if (roles.Contains("IK"))
                    return RedirectToAction("Index", "IK");
                else if (roles.Contains("ITYoneticisi"))
                    return RedirectToAction("Index", "ITYoneticisi");

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Geçersiz email veya şifre.");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            ViewBag.Birimler = await _context.Birimler.Where(b => b.Aktif).OrderBy(b => b.Ad).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string email, string password, string ad, string soyad, string role, int? birimId)
        {
            ViewBag.Birimler = await _context.Birimler.Where(b => b.Aktif).OrderBy(b => b.Ad).ToListAsync();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email ve şifre gereklidir.");
                return View();
            }

            
            string kullaniciAdi = ad;
            string kullaniciSoyadi = soyad;
            
            if (string.IsNullOrWhiteSpace(kullaniciAdi))
            {
                // Email'den @ öncesi kısmı al
                var emailKisim = email.Split('@')[0];
                
                if (!string.IsNullOrEmpty(emailKisim))
                {
                    kullaniciAdi = char.ToUpper(emailKisim[0]) + (emailKisim.Length > 1 ? emailKisim.Substring(1).ToLower() : "");
                }
            }
            
            if (string.IsNullOrWhiteSpace(kullaniciSoyadi))
            {
                kullaniciSoyadi = "Kullanıcı"; 
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Ad = kullaniciAdi,
                Soyad = kullaniciSoyadi,
                Aktif = true,
                OlusturmaTarihi = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(role) && await _roleManager.RoleExistsAsync(role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                }

                if (role == "Stajyer")
                {
                    var gecerliBirimId = (birimId.HasValue && await _context.Birimler.AnyAsync(b => b.Id == birimId.Value && b.Aktif)) ? birimId.Value : 1;
                    var stajyer = new Stajyer
                    {
                        UserId = user.Id,
                        BaslangicTarihi = DateTime.Now,
                        BitisTarihi = DateTime.Now.AddMonths(3),
                        ToplamGun = 90,
                        TamamlananGun = 0,
                        DevamOrani = 0,
                        BirimId = gecerliBirimId,
                        Aktif = true,
                        OlusturmaTarihi = DateTime.Now
                    };

                    _context.Stajyerler.Add(stajyer);
                    await _context.SaveChangesAsync();
                }
                else if (role == "BirimSorumlusu")
                {
                    if (!birimId.HasValue)
                    {
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError("", "Birim Sorumlusu olarak kayıt için birim seçiniz.");
                        return View();
                    }
                    if (!await _context.Birimler.AnyAsync(b => b.Id == birimId.Value && b.Aktif))
                    {
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError("", "Geçersiz birim seçimi.");
                        return View();
                    }
                    _context.BirimSorumlulari.Add(new BirimSorumlusu
                    {
                        UserId = user.Id,
                        BirimId = birimId.Value,
                        AtamaTarihi = DateTime.Now,
                        Aktif = true
                    });
                    await _context.SaveChangesAsync();
                }

            
            TempData["Success"] = "Kayıt işlemi başarıyla tamamlandı. Lütfen giriş yapın.";
            return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}