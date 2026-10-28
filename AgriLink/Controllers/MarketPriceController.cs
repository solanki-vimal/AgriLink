using AgriLink.Data;
using AgriLink.Models;
using AgriLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MarketPriceController : Controller
    {
        private const string ManualSource = "Manual";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMarketPriceProvider _provider;
        private readonly IConfiguration _config;

        public MarketPriceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMarketPriceProvider provider,
            IConfiguration config)
        {
            _context = context;
            _userManager = userManager;
            _provider = provider;
            _config = config;
        }

        // GET: MarketPrice
        public async Task<IActionResult> Index()
        {
            var prices = await _context.MarketPrices
                .Include(mp => mp.Category)
                .Include(mp => mp.SetByAdmin)
                .OrderByDescending(mp => mp.Date)
                    .ThenBy(mp => mp.CommodityName)
                .ToListAsync();

            ViewBag.State = _config["MarketPrices:State"] ?? "Gujarat";
            return View(prices);
        }

        // GET: MarketPrice/Sync (safe fallback if accessed via GET or reloaded)
        [HttpGet]
        public IActionResult Sync() => RedirectToAction(nameof(Index));

        // POST: MarketPrice/Sync
        // No commodity posted  -> sync every commodity in MarketPrices:TrackedCommodities
        // Commodity posted     -> sync just that one
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync(string? commodity)
        {
            var state = _config["MarketPrices:State"] ?? "Gujarat";

            var commodities = string.IsNullOrWhiteSpace(commodity)
                ? (_config.GetSection("MarketPrices:TrackedCommodities").Get<string[]>() ?? Array.Empty<string>())
                : new[] { commodity };

            commodities = commodities
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (commodities.Length == 0)
            {
                TempData["StatusMessage"] = "No commodities to sync. Add some under MarketPrices:TrackedCommodities in appsettings.json.";
                return RedirectToAction(nameof(Index));
            }

            var adminId = _userManager.GetUserId(User)!;
            int added = 0, updated = 0, keptManual = 0;
            var noData = new List<string>();
            string? failure = null;

            foreach (var name in commodities)
            {
                ProviderPrice? price;
                try
                {
                    price = await _provider.GetLatestPriceAsync(name, state);
                }
                catch (MarketPriceProviderException ex)
                {
                    // The source itself is failing - stop instead of timing out once per commodity.
                    failure = ex.Message;
                    break;
                }

                if (price == null)
                {
                    noData.Add(name);
                    continue;
                }

                var dayStart = price.Date.Date;
                var dayEnd = dayStart.AddDays(1);

                var existing = await _context.MarketPrices.FirstOrDefaultAsync(mp =>
                    mp.CommodityName == name && mp.Date >= dayStart && mp.Date < dayEnd);

                if (existing == null)
                {
                    _context.MarketPrices.Add(new MarketPrice
                    {
                        CommodityName = name,
                        Date = dayStart,
                        PricePerUnit = price.PricePerKg,
                        Unit = "Kg",
                        MinPrice = price.MinPricePerKg,
                        MaxPrice = price.MaxPricePerKg,
                        MarketCount = price.MarketCount,
                        Source = _provider.SourceName,
                        SetByAdminId = adminId
                    });
                    added++;
                }
                else if (existing.Source == ManualSource)
                {
                    // An admin typed this rate - never overwrite it with feed data.
                    keptManual++;
                }
                else
                {
                    existing.PricePerUnit = price.PricePerKg;
                    existing.Unit = "Kg";
                    existing.MinPrice = price.MinPricePerKg;
                    existing.MaxPrice = price.MaxPricePerKg;
                    existing.MarketCount = price.MarketCount;
                    existing.SetByAdminId = adminId;
                    updated++;
                }
            }

            await _context.SaveChangesAsync(); // keep whatever succeeded before any failure

            var message = $"{_provider.SourceName} sync ({state}): {added} added, {updated} updated";
            if (keptManual > 0) message += $", {keptManual} kept (manual entry exists)";
            if (noData.Count > 0) message += $". No data for: {string.Join(", ", noData)}";
            if (failure != null) message += $". Stopped early: {failure}";
            TempData["StatusMessage"] = message + ".";

            return RedirectToAction(nameof(Index));
        }

        // GET: MarketPrice/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesDropDown();
            return View(new MarketPrice { Date = DateTime.Today, Unit = "Kg" });
        }

        // POST: MarketPrice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CommodityName,CategoryId,Date,PricePerUnit,Unit")] MarketPrice marketPrice)
        {
            ClearServerSetFieldErrors();
            marketPrice.CommodityName = marketPrice.CommodityName?.Trim() ?? "";

            if (marketPrice.CommodityName.Length > 0 &&
                await DuplicateExistsAsync(marketPrice.CommodityName, marketPrice.Date, null))
            {
                ModelState.AddModelError(string.Empty,
                    "A market price for this commodity and date already exists. Edit the existing entry instead.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesDropDown(marketPrice.CategoryId);
                return View(marketPrice);
            }

            // Server-controlled fields - never taken from the form
            marketPrice.Date = marketPrice.Date.Date;
            marketPrice.SetByAdminId = _userManager.GetUserId(User)!;
            marketPrice.Source = ManualSource;

            _context.MarketPrices.Add(marketPrice);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Market price for {marketPrice.CommodityName} added.";
            return RedirectToAction(nameof(Index));
        }

        // GET: MarketPrice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var marketPrice = await _context.MarketPrices.FindAsync(id);
            if (marketPrice == null) return NotFound();

            await PopulateCategoriesDropDown(marketPrice.CategoryId);
            return View(marketPrice);
        }

        // POST: MarketPrice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("MarketPriceId,CommodityName,CategoryId,Date,PricePerUnit,Unit")] MarketPrice posted)
        {
            if (id != posted.MarketPriceId) return NotFound();

            var existing = await _context.MarketPrices.FindAsync(id);
            if (existing == null) return NotFound();

            ClearServerSetFieldErrors();
            posted.CommodityName = posted.CommodityName?.Trim() ?? "";

            if (posted.CommodityName.Length > 0 &&
                await DuplicateExistsAsync(posted.CommodityName, posted.Date, id))
            {
                ModelState.AddModelError(string.Empty,
                    "Another market price entry already exists for this commodity and date.");
            }

            if (!ModelState.IsValid)
            {
                // Show the stored Source/range in the form's context, not the posted blanks
                await PopulateCategoriesDropDown(posted.CategoryId);
                return View(posted);
            }

            existing.CommodityName = posted.CommodityName;
            existing.CategoryId = posted.CategoryId;
            existing.Date = posted.Date.Date;
            existing.PricePerUnit = posted.PricePerUnit;
            existing.Unit = posted.Unit;
            existing.SetByAdminId = _userManager.GetUserId(User)!;

            // An admin edit makes this a manual rate: the feed's range no longer
            // describes it, and the next sync must not overwrite the correction.
            existing.Source = ManualSource;
            existing.MinPrice = null;
            existing.MaxPrice = null;
            existing.MarketCount = null;

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Market price for {existing.CommodityName} updated (now a manual entry).";
            return RedirectToAction(nameof(Index));
        }

        // GET: MarketPrice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var marketPrice = await _context.MarketPrices
                .Include(mp => mp.Category)
                .FirstOrDefaultAsync(mp => mp.MarketPriceId == id);

            if (marketPrice == null) return NotFound();

            return View(marketPrice);
        }

        // POST: MarketPrice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var marketPrice = await _context.MarketPrices.FindAsync(id);
            if (marketPrice == null) return NotFound();

            _context.MarketPrices.Remove(marketPrice);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Market price entry deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ----- Helpers -----

        // These are set on the server, so the form never posts them. Without this,
        // MVC reports them as "required" and the form can never validate.
        private void ClearServerSetFieldErrors()
        {
            ModelState.Remove(nameof(MarketPrice.SetByAdminId));
            ModelState.Remove(nameof(MarketPrice.SetByAdmin));
            ModelState.Remove(nameof(MarketPrice.Category));
            ModelState.Remove(nameof(MarketPrice.Source));
        }

        private async Task<bool> DuplicateExistsAsync(string commodity, DateTime date, int? excludeId)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            return await _context.MarketPrices.AnyAsync(mp =>
                mp.CommodityName == commodity &&
                mp.Date >= dayStart && mp.Date < dayEnd &&
                (excludeId == null || mp.MarketPriceId != excludeId));
        }

        private async Task PopulateCategoriesDropDown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "CategoryId", "Name", selectedId);
        }
    }
}
