using Microsoft.AspNetCore.SignalR;
using PPMEnergyWeb.Hubs;
using System.Collections.Concurrent;

namespace PPMEnergyWeb.Middleware
{
    public class VisitorTrackerMiddleware
    {
        private readonly RequestDelegate _next;
        // ใช้ ConcurrentDictionary เพื่อความปลอดภัยเมื่อมีคนเข้าพร้อมกันเยอะๆ
        private static readonly ConcurrentDictionary<string, byte> _activeUsers = new();
        private static int _todayViews = 0;
        private static DateTime _lastResetDate = DateTime.Today;

        public VisitorTrackerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IHubContext<QuoteHub> hubContext)
        {
            // ตรวจสอบว่าเป็น Request หน้าเว็บจริงๆ (ไม่ใช่พวกไฟล์ .css, .js, .jpg)
            if (!context.Request.Path.Value!.Contains(".") && context.Request.Method == "GET")
            {
                // 1. จัดการยอดวิววันนี้ (Today's Views)
                if (DateTime.Today > _lastResetDate)
                {
                    _todayViews = 0;
                    _lastResetDate = DateTime.Today;
                }
                _todayViews++;

                // 2. จัดการจำนวนคนออนไลน์ (Live Visitors)
                string connectionId = context.Connection.Id;
                _activeUsers.TryAdd(connectionId, 0);

                // 3. ส่งข้อมูลผ่าน SignalR ไปที่ Dashboard
                // เราส่งไป 2 ค่า: จำนวนคนออนไลน์ และ ยอดวิวรวมวันนี้
                await hubContext.Clients.All.SendAsync("UpdateVisitorStats", _activeUsers.Count, _todayViews);
            }

            await _next(context);
        }
    }
}