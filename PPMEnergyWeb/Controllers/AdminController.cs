using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Text.Json;
using System.Linq;

namespace PPMEnergyWeb.Controllers
{
    [Authorize(Roles = "9")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;

            // ==========================================
            // 1. การ์ดสรุปผลด้านบน (Summary Cards)
            // ==========================================

            // รายการขอใบเสนอราคา (Quotations)
            ViewBag.PendingQuotes = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.New);
            ViewBag.CompletedQuotesThisMonth = await _context.Quotes
                .CountAsync(q => q.Status == QuoteStatus.Won && q.CreatedDate.Month == now.Month && q.CreatedDate.Year == now.Year);

            // จัดการสินค้า (Products)
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.IncompleteProducts = await _context.Products
                .CountAsync(p => string.IsNullOrEmpty(p.ImageUrl) || p.CategoryId == null);

            // รายชื่อลูกค้า (Customers)
            ViewBag.TotalCustomers = await _context.Users.CountAsync();
            // ลูกค้าใหม่เดือนนี้ (สมมติว่าตรวจสอบจากใบเสนอราคาแรก หรือถ้ามีฟิลด์วันที่สมัครในตาราง Users ให้ปรับใช้แทนได้ครับ)
            ViewBag.NewCustomersThisMonth = await _context.Quotes
                .Where(q => q.CreatedDate.Month == now.Month && q.CreatedDate.Year == now.Year)
                .Select(q => q.Email)
                .Distinct()
                .CountAsync();

            // จัดการหมวดหมู่ (Categories)
            ViewBag.TotalCategories = await _context.Categories.CountAsync();


            // ==========================================
            // 2. ข้อมูลกราฟวิเคราะห์ (Data Visualization)
            // ==========================================

            // 📊 2.1 กราฟโดนัท - สัดส่วนสถานะใบเสนอราคา
            var quoteStatuses = await _context.Quotes
                .GroupBy(q => q.Status)
                .Select(g => new { StatusName = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusLabels = JsonSerializer.Serialize(quoteStatuses.Select(s => s.StatusName));
            ViewBag.StatusData = JsonSerializer.Serialize(quoteStatuses.Select(s => s.Count));

            // 📈 2.2 กราฟแท่ง - 5 สินค้ายอดนิยม (ใช้ Join เพื่อดึงชื่อสินค้าหลัก)
            var topProducts = await _context.QuoteDetails
                .Join(_context.Products,
                    qd => qd.ProductId,  // ดึงรหัสสินค้าจากตารางรายละเอียดใบเสนอราคา
                    p => p.Id,           // มาเทียบกับรหัสสินค้าในตารางสินค้าหลัก
                    (qd, p) => new { QuoteDetail = qd, Product = p }
                )
                .GroupBy(x => new { x.Product.Id, x.Product.Name }) // จัดกลุ่มด้วย ID จะได้แม่นยำที่สุด
                .Select(g => new {
                    ProductName = g.Key.Name, // ใช้ชื่อแบบ Official จากตาราง Products
                    QuoteCount = g.Select(x => x.QuoteDetail.QuoteId).Distinct().Count() // นับจำนวนใบเสนอราคา
                })
                .OrderByDescending(x => x.QuoteCount)
                .Take(5)
                .ToListAsync();

            ViewBag.TopProductLabels = JsonSerializer.Serialize(topProducts.Select(p => p.ProductName));
            ViewBag.TopProductData = JsonSerializer.Serialize(topProducts.Select(p => p.QuoteCount));

            // 📉 2.3 เพิ่มใหม่: กราฟเส้น - แนวโน้มการขอใบเสนอราคา 7 วันล่าสุด (Quotation Trends)
            var startDate = DateTime.Today.AddDays(-6);
            var trendDataRaw = await _context.Quotes
                .Where(q => q.CreatedDate >= startDate)
                .GroupBy(q => q.CreatedDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var trendLabels = new List<string>();
            var trendCounts = new List<int>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                trendLabels.Add(date.ToString("dd MMM"));
                var match = trendDataRaw.FirstOrDefault(d => d.Date == date);
                trendCounts.Add(match?.Count ?? 0);
            }
            ViewBag.TrendLabels = JsonSerializer.Serialize(trendLabels);
            ViewBag.TrendData = JsonSerializer.Serialize(trendCounts);


            // ==========================================
            // 3. ตารางรายการล่าสุดด้านล่าง (Recent Quotes)
            // ==========================================
            var latestQuotes = await _context.Quotes
                .OrderByDescending(q => q.CreatedDate)
                .Take(5)
                .ToListAsync();

            return View(latestQuotes);
        }
    }
}