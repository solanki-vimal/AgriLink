using AgriLink.Data;
using AgriLink.Models;
using AgriLink.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    public class BrowseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BrowseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Browse
        public async Task<IActionResult> Index(
            string? searchTerm,
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? location,
            string sortBy = "newest")
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var query = _context.ProduceListings
                .Include(p => p.Category)
                .Include(p => p.Farmer)
                .Where(p => p.Status == ProduceStatus.Available && p.Quantity > 0)
                .AsQueryable();

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    (p.Description != null && p.Description.ToLower().Contains(term)) ||
                    p.Location.ToLower().Contains(term));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue && minPrice.Value >= 0)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var loc = location.Trim().ToLower();
                query = query.Where(p => p.Location.ToLower().Contains(loc));
            }

            // Apply Sorting
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "harvest_desc" => query.OrderByDescending(p => p.HarvestDate),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var listings = await query.ToListAsync();

            var viewModel = new BrowseViewModel
            {
                ProduceListings = listings,
                Categories = categories,
                SearchTerm = searchTerm,
                CategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Location = location,
                SortBy = sortBy,
                TotalResultsCount = listings.Count
            };

            return View(viewModel);
        }

        // GET: Browse/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var produce = await _context.ProduceListings
                .Include(p => p.Category)
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.ProduceId == id);

            if (produce == null)
            {
                return NotFound();
            }

            // Farmer Profile
            var farmerProfile = await _context.FarmerProfiles
                .FirstOrDefaultAsync(fp => fp.UserId == produce.FarmerId);

            // Price History for this specific produce
            var priceHistories = await _context.PriceHistories
                .Where(ph => ph.ProduceId == produce.ProduceId)
                .OrderBy(ph => ph.ChangedAt)
                .ToListAsync();

            // Reference Market Price for category
            var marketBenchmark = await _context.MarketPrices
                .Where(mp => mp.CategoryId == produce.CategoryId)
                .OrderByDescending(mp => mp.Date)
                .FirstOrDefaultAsync();

            // Prepare Chart.js data
            var chartLabels = new List<string>();
            var chartPrices = new List<decimal>();

            if (priceHistories.Any())
            {
                foreach (var ph in priceHistories)
                {
                    chartLabels.Add(ph.ChangedAt.ToString("MMM dd"));
                    chartPrices.Add(ph.NewPrice);
                }
            }
            else
            {
                // Initial baseline price point
                chartLabels.Add(produce.CreatedAt.ToString("MMM dd"));
                chartPrices.Add(produce.Price);
            }

            var viewModel = new ProduceDetailsViewModel
            {
                Produce = produce,
                FarmerProfile = farmerProfile,
                PriceHistories = priceHistories,
                MarketPriceBenchmark = marketBenchmark,
                ChartLabels = chartLabels,
                ChartPrices = chartPrices
            };

            return View(viewModel);
        }
    }
}
