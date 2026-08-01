using Microsoft.AspNetCore.Identity.UI.Services;

namespace PPMEnergyWeb.Services
{
    // ════════════════════════════════════════════════════════════
    // ตัวเชื่อม (Adapter) ให้ ASP.NET Identity ใช้ EmailService เดิม
    // (ตัวที่ใช้ MailKit ส่งอีเมลใบเสนอราคาอยู่แล้ว) ได้โดยตรง
    //
    // เหตุผล: Identity (ตอน Forgot Password) ต้องการ interface
    // ชื่อ IEmailSender ของ Microsoft ส่วน QuoteController เรียกใช้
    // IEmailService ของโปรเจคเอง — แทนที่จะเขียนโค้ดส่ง SMTP ซ้ำอีกชุด
    // เราแค่ "ห่อ" เรียก EmailService ตัวเดิมที่ทำงานอยู่แล้วแทน
    // ทำให้ทั้งระบบส่งอีเมลผ่านโค้ดเดียว (MailKit) จุดเดียว
    // ════════════════════════════════════════════════════════════
    public class IdentityEmailAdapter : IEmailSender
    {
        private readonly IEmailService _emailService;

        public IdentityEmailAdapter(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            return _emailService.SendEmailAsync(toEmail, subject, htmlMessage);
        }
    }
}
