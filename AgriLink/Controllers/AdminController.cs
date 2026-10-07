using AgriLink.Data;
using AgriLink.Helpers;
using AgriLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Build lookups of UserId -> role name and UserId -> locked-out status
            var roleNames = new Dictionary<string, string>();
            var lockedOutStatus = new Dictionary<string, bool>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                roleNames[user.Id] = roles.FirstOrDefault() ?? "—";
                lockedOutStatus[user.Id] = await _userManager.IsLockedOutAsync(user);
            }

            ViewBag.RoleNames = roleNames;
            ViewBag.LockedOutStatus = lockedOutStatus;
            return View(users);
        }

        // POST: Admin/ToggleLockout/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLockout(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // An admin can't lock themselves out of the system
            if (id == _userManager.GetUserId(User))
            {
                TempData["StatusMessage"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Users));
            }

            var isCurrentlyLockedOut = await _userManager.IsLockedOutAsync(user);

            if (isCurrentlyLockedOut)
            {
                // Activate — clear the lockout
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["StatusMessage"] = $"{user.FullName} has been reactivated.";
            }
            else
            {
                // Deactivate — lock out far enough in the future to be effectively permanent
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                TempData["StatusMessage"] = $"{user.FullName} has been deactivated.";
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/DeleteUser/{id}
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (id == _userManager.GetUserId(User))
            {
                TempData["StatusMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "—";
            ViewBag.Role = role;

            return View(user);
        }

        // POST: Admin/DeleteUser/{id}
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (id == _userManager.GetUserId(User))
            {
                TempData["StatusMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            // Check for existing activity before attempting delete — Produce.FarmerId,
            // Order.BuyerId, and MarketPrice.SetByAdminId are all Restrict, not Cascade,
            // so a user with history can't simply be removed.
            var hasListings = await _context.ProduceListings.AnyAsync(p => p.FarmerId == id);
            var hasOrders = await _context.Orders.AnyAsync(o => o.BuyerId == id);
            var hasSetPrices = await _context.MarketPrices.AnyAsync(mp => mp.SetByAdminId == id);

            if (hasListings || hasOrders || hasSetPrices)
            {
                TempData["StatusMessage"] =
                    $"Cannot delete {user.FullName} — they have existing listings, orders, or market price entries. Deactivate instead.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);

            TempData["StatusMessage"] = result.Succeeded
                ? $"{user.FullName} has been deleted."
                : $"Could not delete {user.FullName}: {string.Join(", ", result.Errors.Select(e => e.Description))}";

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/Listings
        public async Task<IActionResult> Listings()
        {
            var listings = await _context.ProduceListings
                .Include(p => p.Category)
                .Include(p => p.Farmer)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(listings);
        }

        // GET: Admin/DeleteListing/5
        public async Task<IActionResult> DeleteListing(int? id)
        {
            if (id == null) return NotFound();

            var produce = await _context.ProduceListings
                .Include(p => p.Category)
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.ProduceId == id);

            if (produce == null) return NotFound();

            return View(produce);
        }

        // POST: Admin/DeleteListing/5
        [HttpPost, ActionName("DeleteListing")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteListingConfirmed(int id)
        {
            var produce = await _context.ProduceListings.FindAsync(id);
            if (produce == null) return NotFound();

            try
            {
                _context.ProduceListings.Remove(produce);
                await _context.SaveChangesAsync();
                ImageUploadHelper.DeleteImage(produce.ImageUrl, _environment);
                TempData["StatusMessage"] = $"Listing \"{produce.Name}\" removed by admin.";
            }
            catch (DbUpdateException)
            {
                TempData["StatusMessage"] =
                    $"Cannot remove \"{produce.Name}\" — it has existing orders against it.";
            }

            return RedirectToAction(nameof(Listings));
        }

        // GET: Admin/Orders
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.Produce)
                .Include(o => o.Buyer)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }
    }
}