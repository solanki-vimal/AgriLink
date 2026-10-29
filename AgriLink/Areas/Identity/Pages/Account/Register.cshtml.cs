using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using AgriLink.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AgriLink.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(100, MinimumLength = 3)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Address is required.")]
            [StringLength(200)]
            public string Address { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please select a role.")]
            [Display(Name = "I am registering as a")]
            public string Role { get; set; } = string.Empty; // "Farmer" or "Buyer" only — Admin is not self-service
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Only Farmer/Buyer can self-register — reject anything else
            // even if the form was tampered with client-side.
            if (Input.Role != "Farmer" && Input.Role != "Buyer")
            {
                ModelState.AddModelError(string.Empty, "Invalid role selected.");
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName,
                Address = Input.Address,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, Input.Role);

                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);

                var emailBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <h2 style='color: #2e7d32; margin-top: 0;'>Welcome to AgriLink!</h2>
    <p>Hello <strong>{Input.FullName}</strong>,</p>
    <p>Thank you for registering as a <strong>{Input.Role}</strong> on AgriLink. Please confirm your email address to activate your account:</p>
    <div style='margin: 30px 0; text-align: center;'>
        <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Confirm Email Address</a>
    </div>
    <p style='color: #666; font-size: 14px;'>Or copy and paste this link into your browser:</p>
    <p style='word-break: break-all; color: #2e7d32; font-size: 13px;'><a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>{HtmlEncoder.Default.Encode(callbackUrl!)}</a></p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 24px 0;' />
    <p style='color: #999; font-size: 12px; margin-bottom: 0;'>If you did not create an account on AgriLink, you can safely ignore this email.</p>
</div>";

                try
                {
                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your AgriLink account", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending confirmation email to {Email}", Input.Email);
                }

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                }
                else
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}
