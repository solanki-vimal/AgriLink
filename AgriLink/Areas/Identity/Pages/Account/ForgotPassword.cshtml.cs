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
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<ForgotPasswordModel> logger)
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

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                var emailBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 8px;'>
    <div style='background-color: #2e7d32; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; margin: -24px -24px 20px -24px;'>
        <h2 style='margin: 0; font-size: 20px;'>AgriLink · Reset Your Password</h2>
    </div>
    <p>Hello <strong>{user.FullName}</strong>,</p>
    <p>We received a request to reset the password for your AgriLink account. Click the button below to choose a new password:</p>

    <div style='text-align: center; margin: 30px 0;'>
        <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>
            Reset Password
        </a>
    </div>

    <p style='color: #666; font-size: 14px;'>Or copy and paste this link into your browser:</p>
    <p style='word-break: break-all; color: #2e7d32; font-size: 13px;'><a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>{HtmlEncoder.Default.Encode(callbackUrl!)}</a></p>

    <hr style='border: none; border-top: 1px solid #eee; margin: 24px 0;' />
    <p style='color: #999; font-size: 12px; margin-bottom: 0;'>If you did not request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>
</div>";

                try
                {
                    await _emailSender.SendEmailAsync(Input.Email, "Reset Your AgriLink Password", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email to {Email}", Input.Email);
                }

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
