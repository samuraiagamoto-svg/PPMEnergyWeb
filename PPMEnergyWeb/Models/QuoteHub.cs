using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Hubs
{
    public class QuoteHub : Hub
    {
        // แอดมินจะ Subscribe รอรับข้อความผ่าน Method "ReceiveNewQuote"
        public async Task SendNewQuoteNotification(string companyName, string productName)
        {
            await Clients.All.SendAsync("ReceiveNewQuote", companyName, productName);
        }
    }
}