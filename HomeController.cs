using System.Diagnostics;
using DataAccessLayer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using stajyer_takip.Models;

namespace stajyer_takip.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                
                if (User.IsInRole("Stajyer"))
                    return RedirectToAction("Index", "Stajyer");
                else if (User.IsInRole("BirimSorumlusu"))
                    return RedirectToAction("Index", "BirimSorumlusu");
                else if (User.IsInRole("IK"))
                    return RedirectToAction("Index", "IK");
                else if (User.IsInRole("ITYoneticisi"))
                    return RedirectToAction("Index", "ITYoneticisi");
            }

            // Anasayfada gösterilecek tamamlanan projeler (stajyer panelinden eklenenler)
            var tamamlananProjeler = await _context.StajyerProjeler
                .Include(p => p.Stajyer)
                    .ThenInclude(s => s.User)
                .OrderByDescending(p => p.OlusturmaTarihi)
                .Take(30)
                .ToListAsync();

            ViewBag.TamamlananProjeler = tamamlananProjeler;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
