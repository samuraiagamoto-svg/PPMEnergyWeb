using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(emailSettings["SenderName"], emailSettings["SenderEmail"]));
            emailMessage.To.Add(new MailboxAddress("Admin", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                // เชื่อมต่อ Gmail Server ด้วย STARTTLS (Port 587)
                await client.ConnectAsync(
                    emailSettings["SmtpServer"],
                    int.Parse(emailSettings["Port"]),
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                // ยืนยันตัวตนด้วยเมลบอทและรหัสแอป 16 หลักที่คุณตั้งไว้
                await client.AuthenticateAsync(emailSettings["SenderEmail"], emailSettings["SenderPassword"]);

                // สั่งส่งอีเมลแจ้งเตือน
                await client.SendAsync(emailMessage);

                // ตัดการเชื่อมต่ออย่างปลอดภัย
                await client.DisconnectAsync(true);
            }
        }
    }
}