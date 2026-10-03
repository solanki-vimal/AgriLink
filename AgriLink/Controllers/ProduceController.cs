using AgriLink.Data;
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

        // GET: Produce
        public async Task<IActionResult> Index()
        {
            var farmerId = _userManager.GetUserId(User);
            var listings = await _context.ProduceListings
                .Include(p => p.Category)
                .Where(p => p.FarmerId == farmerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(listings);
        }

        // GET: Produce/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var viewModel = new ProduceFormViewModel
            {
                HarvestDate = DateTime.Today,
                Categories = await GetCategorySelectListAsync()
            };

            return View(viewModel);
        }

        // POST: Produce/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProduceFormViewModel model)
        {
            var farmerId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(farmerId))
            {
                return Challenge();
            }

            if (ModelState.IsValid)
            {
                string? imageUrl = null;
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    imageUrl = await SaveProduceImageAsync(model.ImageFile);
                }

                var produce = new Produce
                {
                    FarmerId = farmerId,
                    CategoryId = model.CategoryId,
                    Name = model.Name,
                    Description = model.Description,
                    Quantity = model.Quantity,
                    Unit = model.Unit,
                    Price = model.Price,
                    HarvestDate = model.HarvestDate,
                    Location = model.Location,
                    ImageUrl = imageUrl,
                    Status = ProduceStatus.Available,
                    CreatedAt = DateTime.Now
                };

                _context.ProduceListings.Add(produce);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Produce listing \"{produce.Name}\" created successfully.";
                return RedirectToAction(nameof(Index));
            }

            model.Categories = await GetCategorySelectListAsync(model.CategoryId);
            return View(model);
        }

        // GET: Produce/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null)
            {
                return NotFound();
            }

            // Ownership check: only the listing owner can edit
            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var viewModel = new ProduceFormViewModel
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
                ExistingImageUrl = produce.ImageUrl,
                Categories = await GetCategorySelectListAsync(produce.CategoryId)
            };

            return View(viewModel);
        }

        // POST: Produce/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProduceFormViewModel model)
        {
            if (id != model.ProduceId)
            {
                return NotFound();
            }

            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null)
            {
                return NotFound();
            }

            // Ownership check: only the listing owner can edit
            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                // Auto-log to PriceHistory only when price changes
                if (model.Price != produce.Price)
                {
                    var priceHistory = new PriceHistory
                    {
                        ProduceId = produce.ProduceId,
                        OldPrice = produce.Price,
                        NewPrice = model.Price,
                        ChangedAt = DateTime.Now
                    };
                    _context.PriceHistories.Add(priceHistory);
                    produce.Price = model.Price;
                }

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    produce.ImageUrl = await SaveProduceImageAsync(model.ImageFile);
                }

                produce.CategoryId = model.CategoryId;
                produce.Name = model.Name;
                produce.Description = model.Description;
                produce.Quantity = model.Quantity;
                produce.Unit = model.Unit;
                produce.HarvestDate = model.HarvestDate;
                produce.Location = model.Location;

                try
                {
                    _context.ProduceListings.Update(produce);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.ProduceListings.AnyAsync(p => p.ProduceId == id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                TempData["StatusMessage"] = $"Produce listing \"{produce.Name}\" updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            model.ExistingImageUrl = produce.ImageUrl;
            model.Categories = await GetCategorySelectListAsync(model.CategoryId);
            return View(model);
        }

        // POST: Produce/ToggleSoldOut/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSoldOut(int id)
        {
            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null)
            {
                return NotFound();
            }

            // Ownership check: only the listing owner can toggle status
            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            produce.Status = produce.Status == ProduceStatus.Available
                ? ProduceStatus.SoldOut
                : ProduceStatus.Available;

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Produce \"{produce.Name}\" marked as {(produce.Status == ProduceStatus.Available ? "Available" : "Sold Out")}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Produce/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var produce = await _context.ProduceListings
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProduceId == id);

            if (produce == null)
            {
                return NotFound();
            }

            // Ownership check: only the listing owner can delete
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
            if (produce == null)
            {
                return NotFound();
            }

            // Ownership check: only the listing owner can delete
            if (produce.FarmerId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            try
            {
                _context.ProduceListings.Remove(produce);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"Produce listing \"{produce.Name}\" deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["StatusMessage"] = $"Cannot delete \"{produce.Name}\" — it is still linked to existing orders.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetCategorySelectListAsync(int? selectedId = null)
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            return categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.Name,
                Selected = selectedId.HasValue && c.CategoryId == selectedId.Value
            });
        }

        private async Task<string> SaveProduceImageAsync(IFormFile imageFile)
        {
            var uploadDir = Path.Combine(_environment.WebRootPath, "images", "produce");
            Directory.CreateDirectory(uploadDir);

            var extension = Path.GetExtension(imageFile.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadDir, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/produce/{uniqueFileName}";
        }
    }
}
