using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace PPMEnergyWeb.Services
{
    // ── ค่า config สำหรับ SMTP (อ่านจาก appsettings.json) ──────────
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = string.Empty;   // เช่น ppmenergy@gmail.com
        public string SenderName { get; set; } = "PPM Energy Thailand";
        public string AppPassword { get; set; } = string.Empty;   // App Password 16 หลักจาก Google
    }

    // ── ตัวส่งอีเมลจริง ผ่าน Gmail SMTP ─────────────────────────────
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<EmailSettings> settings, ILogger<EmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            try
            {
                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_settings.SenderEmail, _settings.AppPassword),
                    EnableSsl = true
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                // อย่าให้ exception หลุดออกไปจนระบบ error ตรงๆ — log ไว้พอ
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }
    }
}
