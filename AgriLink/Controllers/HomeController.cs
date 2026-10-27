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

            var totalFarmers = await (from ur in _context.UserRoles
                                      join r in _context.Roles on ur.RoleId equals r.Id
                                      where r.NormalizedName == "FARMER" || r.Name == "Farmer"
                                      select ur.UserId)
                                     .Distinct()
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

        // Custom 404 handler
        [Route("Home/NotFound404")]
        public IActionResult NotFound404()
        {
            Response.StatusCode = 404;
            return View("NotFound404");
        }

        // Centralized HTTP status code handler
        [Route("Home/StatusCodeHandler/{statusCode}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCodeHandler(int statusCode)
        {
            if (statusCode == 404)
            {
                Response.StatusCode = 404;
                return View("NotFound404");
            }

            var vm = new ErrorViewModel
            {
                StatusCode = statusCode,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Title = statusCode switch
                {
                    401 => "Unauthorized Access",
                    403 => "Access Denied",
                    500 => "Internal Server Error",
                    _ => $"Error {statusCode}"
                },
                Message = statusCode switch
                {
                    401 => "You must be signed in to view this resource.",
                    403 => "You do not have permission to view or manage this resource.",
                    500 => "An unexpected server error occurred. Our team has been notified.",
                    _ => "An error occurred while processing your request."
                }
            };

            return View("Error", vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = 500,
                Title = "Unexpected Error",
                Message = "An unexpected error occurred while processing your request. Please try again or return to the home page."
            });
        }
    }
}
