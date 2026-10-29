using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace AgriLink.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            var portString = _configuration["Email:SmtpPort"];
            int port = int.TryParse(portString, out var p) ? p : 587;
            var senderEmail = _configuration["Email:SenderEmail"] ?? _configuration["Email:SmtpUser"];
            var senderName = _configuration["Email:SenderName"] ?? "AgriLink";
            var password = _configuration["Email:Password"] ?? _configuration["Email:SmtpPass"];

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Email sending skipped: Email credentials (SenderEmail / Password) not configured.");
                return;
            }

            try
            {
                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email '{Subject}' sent successfully to {Email}", subject, email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email '{Subject}' to {Email}", subject, email);
                throw;
            }
        }
    }
}
