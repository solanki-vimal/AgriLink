using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using AgriLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AgriLink.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ResendEmailConfirmationModel> _logger;

        public ResendEmailConfirmationModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<ResendEmailConfirmationModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet(string? email = null)
        {
            if (!string.IsNullOrEmpty(email))
            {
                Input.Email = email;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Verification email sent. Please check your inbox.");
                return Page();
            }

            if (user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "This email is already confirmed. You can log in directly.");
                return Page();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId = userId, code = code },
                protocol: Request.Scheme);

            var emailBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <h2 style='color: #2e7d32; margin-top: 0;'>Confirm Your AgriLink Account</h2>
    <p>Hello <strong>{user.FullName}</strong>,</p>
    <p>You requested a new verification link for your AgriLink account. Click the button below to confirm your email:</p>
    <div style='margin: 30px 0; text-align: center;'>
        <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Confirm Email Address</a>
    </div>
    <p style='color: #666; font-size: 14px;'>Or copy and paste this link into your browser:</p>
    <p style='word-break: break-all; color: #2e7d32; font-size: 13px;'><a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>{HtmlEncoder.Default.Encode(callbackUrl!)}</a></p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 24px 0;' />
    <p style='color: #999; font-size: 12px; margin-bottom: 0;'>If you did not make this request, you can safely ignore this email.</p>
</div>";

            try
            {
                await _emailSender.SendEmailAsync(Input.Email, "Confirm your AgriLink account", emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending confirmation email to {Email}", Input.Email);
            }

            ModelState.AddModelError(string.Empty, "Verification email sent. Please check your inbox.");
            return Page();
        }
    }
}
