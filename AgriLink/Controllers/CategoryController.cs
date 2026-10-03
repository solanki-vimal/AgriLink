using AgriLink.Data;
using AgriLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Category
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        // GET: Category/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Category \"{category.Name}\" created successfully.";
                return RedirectToAction(nameof(Index));
            }

            // Validation failed — return the same view so error messages show
            return View(category);
        }

        // GET: Category/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.CategoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Categories.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Categories.AnyAsync(c => c.CategoryId == id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                TempData["StatusMessage"] = $"Category \"{category.Name}\" updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // GET: Category/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            // Category -> Produce and Category -> MarketPrice are Restrict,
            // not Cascade, so deleting a category still in use will throw —
            // catch it and show a friendly message instead of a 500 error.
            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"Category \"{category.Name}\" deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["StatusMessage"] =
                    $"Cannot delete \"{category.Name}\" — it is still linked to existing produce listings or market prices.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}