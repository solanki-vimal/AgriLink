using AgriLink.Data;
using AgriLink.Models;
using AgriLink.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AgriLink.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var recentListings = await _context.ProduceListings
                .Include(p => p.Category)
                .Include(p => p.Farmer)
                .Where(p => p.Status == ProduceStatus.Available && p.Quantity > 0)
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .ToListAsync();

            var totalListings = await _context.ProduceListings
                .CountAsync(p => p.Status == ProduceStatus.Available && p.Quantity > 0);

            var totalFarmers = await _context.FarmerProfiles
                .CountAsync();

            var viewModel = new HomeViewModel
            {
                Categories = categories,
                RecentListings = recentListings,
                TotalListingsCount = totalListings,
                TotalFarmersCount = totalFarmers
            };

            return View(viewModel);
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
