using AgriLink.Data;
using AgriLink.Helpers;
using AgriLink.Models;
using AgriLink.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Controllers
{
    [Authorize(Roles = "Farmer")]
    public class FarmerProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public FarmerProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // GET: FarmerProfile  ("My Farm Profile")
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var profile = await _context.FarmerProfiles
                .FirstOrDefaultAsync(fp => fp.UserId == userId);

            // No profile yet — send them straight to the create/edit form instead
            // of showing an empty page with nothing to do.
            if (profile == null)
            {
                return RedirectToAction(nameof(Edit));
            }

            return View(profile);
        }

        // GET: FarmerProfile/Edit  (doubles as Create, since it's 1-to-1)
        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _context.FarmerProfiles
                .FirstOrDefaultAsync(fp => fp.UserId == userId);

            var vm = existing == null
                ? new FarmerProfileFormViewModel { UserId = userId }
                : new FarmerProfileFormViewModel
                {
                    UserId = existing.UserId,
                    FarmName = existing.FarmName,
                    Bio = existing.Bio,
                    FarmLocation = existing.FarmLocation,
                    FarmSizeAcres = existing.FarmSizeAcres,
                    YearsOfExperience = existing.YearsOfExperience,
                    ExistingFarmImageUrl = existing.FarmImageUrl
                };

            return View(vm);
        }

        // POST: FarmerProfile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FarmerProfileFormViewModel vm)
        {
            var userId = _userManager.GetUserId(User);
            // Force UserId server-side — never trust it from the form.
            vm.UserId = userId;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var existing = await _context.FarmerProfiles
                .FirstOrDefaultAsync(fp => fp.UserId == userId);

            string? imageUrl = existing?.FarmImageUrl;

            if (vm.FarmImageFile != null)
            {
                ImageUploadHelper.DeleteImage(imageUrl, _environment);
                imageUrl = await ImageUploadHelper.SaveImageAsync(vm.FarmImageFile, _environment, "farmers");
            }

            if (existing == null)
            {
                var profile = new FarmerProfile
                {
                    UserId = userId,
                    FarmName = vm.FarmName,
                    Bio = vm.Bio,
                    FarmLocation = vm.FarmLocation,
                    FarmSizeAcres = vm.FarmSizeAcres,
                    YearsOfExperience = vm.YearsOfExperience,
                    FarmImageUrl = imageUrl,
                    CreatedAt = DateTime.Now
                };
                _context.FarmerProfiles.Add(profile);
                TempData["StatusMessage"] = "Farm profile created successfully.";
            }
            else
            {
                existing.FarmName = vm.FarmName;
                existing.Bio = vm.Bio;
                existing.FarmLocation = vm.FarmLocation;
                existing.FarmSizeAcres = vm.FarmSizeAcres;
                existing.YearsOfExperience = vm.YearsOfExperience;
                existing.FarmImageUrl = imageUrl;
                TempData["StatusMessage"] = "Farm profile updated successfully.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}