using AgriLink.Data;
using AgriLink.Helpers;
using AgriLink.Models;
using AgriLink.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize(Roles = "Farmer")]
    public class ProduceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ProduceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // GET: Produce  (a Farmer's own listings only — "My Listings")
        public async Task<IActionResult> Index()
        {
            var farmerId = _userManager.GetUserId(User);

            var myListings = await _context.ProduceListings
                .Include(p => p.Category)
                .Where(p => p.FarmerId == farmerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(myListings);
        }

        // GET: Produce/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesDropDown();
            return View(new ProduceFormViewModel());
        }

        // POST: Produce/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProduceFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCategoriesDropDown(vm.CategoryId);
                return View(vm);
            }

            var produce = new Produce
            {
                FarmerId = _userManager.GetUserId(User),
                CategoryId = vm.CategoryId,
                Name = vm.Name,
                Description = vm.Description,
                Quantity = vm.Quantity,
                Unit = vm.Unit,
                Price = vm.Price,
                HarvestDate = vm.HarvestDate,
                Location = vm.Location,
                Status = ProduceStatus.Available,
                CreatedAt = DateTime.Now
            };

            if (vm.ImageFile != null)
            {
                produce.ImageUrl = await ImageUploadHelper.SaveImageAsync(vm.ImageFile, _environment, "produce");
            }

            _context.ProduceListings.Add(produce);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{produce.Name}\" listed successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Produce/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null) return NotFound();

            // Ownership check — a farmer can only edit their own listings
            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var vm = new ProduceFormViewModel
            {
                ProduceId = produce.ProduceId,
                CategoryId = produce.CategoryId,
                Name = produce.Name,
                Description = produce.Description,
                Quantity = produce.Quantity,
                Unit = produce.Unit,
                Price = produce.Price,
                HarvestDate = produce.HarvestDate,
                Location = produce.Location,
                ExistingImageUrl = produce.ImageUrl
            };

            await PopulateCategoriesDropDown(vm.CategoryId);
            return View(vm);
        }

        // POST: Produce/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProduceFormViewModel vm)
        {
            if (id != vm.ProduceId) return NotFound();

            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null) return NotFound();

            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                vm.ExistingImageUrl = produce.ImageUrl;
                await PopulateCategoriesDropDown(vm.CategoryId);
                return View(vm);
            }

            // Log to PriceHistory only if the asking price actually changed
            if (produce.Price != vm.Price)
            {
                _context.PriceHistories.Add(new PriceHistory
                {
                    ProduceId = produce.ProduceId,
                    OldPrice = produce.Price,
                    NewPrice = vm.Price,
                    ChangedAt = DateTime.Now
                });
            }

            produce.CategoryId = vm.CategoryId;
            produce.Name = vm.Name;
            produce.Description = vm.Description;
            produce.Quantity = vm.Quantity;
            produce.Unit = vm.Unit;
            produce.Price = vm.Price;
            produce.HarvestDate = vm.HarvestDate;
            produce.Location = vm.Location;

            if (vm.ImageFile != null)
            {
                // Clean up the old file now that we're replacing it
                ImageUploadHelper.DeleteImage(produce.ImageUrl, _environment);
                produce.ImageUrl = await ImageUploadHelper.SaveImageAsync(vm.ImageFile, _environment, "produce");
            }
            // else: keep the existing ImageUrl untouched

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{produce.Name}\" updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Produce/ToggleSoldOut/5  (quick manual override, per the hybrid design)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSoldOut(int id)
        {
            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null) return NotFound();

            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            produce.Status = produce.Status == ProduceStatus.Available
                ? ProduceStatus.SoldOut
                : ProduceStatus.Available;

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{produce.Name}\" marked as {produce.Status}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Produce/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var produce = await _context.ProduceListings
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProduceId == id);

            if (produce == null) return NotFound();

            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            return View(produce);
        }

        // POST: Produce/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null) return NotFound();

            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            // Produce -> Order is Restrict, so this will throw if orders exist against it
            try
            {
                _context.ProduceListings.Remove(produce);
                await _context.SaveChangesAsync();
                ImageUploadHelper.DeleteImage(produce.ImageUrl, _environment);
                TempData["StatusMessage"] = $"\"{produce.Name}\" deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["StatusMessage"] =
                    $"Cannot delete \"{produce.Name}\" — it has existing orders against it. Mark it Sold Out instead.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Helpers

        private async Task PopulateCategoriesDropDown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "CategoryId", "Name", selectedId);
        }
    }
}