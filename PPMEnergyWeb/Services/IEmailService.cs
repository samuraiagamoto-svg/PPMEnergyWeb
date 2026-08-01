using System.Threading.Tasks;

namespace PPMEnergyWeb.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}