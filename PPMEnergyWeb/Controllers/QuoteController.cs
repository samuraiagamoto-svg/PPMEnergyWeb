using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Claims; // 📍 [NEW] เพิ่มเข้ามาเพื่อใช้ดึงข้อมูล User ที่ล็อกอิน
using PPMEnergyWeb.Models;
using PPMEnergyWeb.Hubs;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Services;
using Microsoft.Extensions.Configuration;

namespace PPMEnergyWeb.Controllers
{
    public class QuoteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<QuoteHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public QuoteController(
            ApplicationDbContext context,
            IHubContext<QuoteHub> hubContext,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _hubContext = hubContext;
            _emailService = emailService;
            _configuration = configuration;
        }

        // ==========================================
        // ส่วนของ Admin (จัดการข้อมูลหลังบ้าน)
        // ==========================================

        // GET: Quote/Index
        public async Task<IActionResult> Index(string search, DateTime? fromDate, QuoteStatus? status)
        {
            // ==========================================
            // 1. ดึงข้อมูล Quote สำหรับแสดงผลในตารางหลัก
            // ==========================================
            var query = _context.Quotes.Include(q => q.QuoteDetails).AsQueryable();

            // กรองตามสถานะ (ค่าเริ่มต้นเมื่อเปิดหน้ามาครั้งแรก = สถานะ "มาใหม่")
            var currentStatus = status ?? QuoteStatus.New;
            query = query.Where(q => q.Status == currentStatus);
            ViewBag.CurrentStatus = currentStatus;

            // ค้นหาด่วน (ชื่อบริษัท, ชื่อผู้ติดต่อ, อีเมล)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(q => q.CompanyName.Contains(search) ||
                                         q.ContactName.Contains(search) ||
                                         q.Email.Contains(search));
            }

            // กรองตามวันที่
            if (fromDate.HasValue)
            {
                query = query.Where(q => q.CreatedDate.Date >= fromDate.Value.Date);
            }

            var quotes = await query.OrderByDescending(q => q.CreatedDate).ToListAsync();

            // ==========================================
            // 2. คำนวณสถิติสำหรับ KPI Cards (คำนวณที่ระดับ Database เพื่อความรวดเร็ว)
            // ==========================================
            ViewBag.NewRequests = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.New);
            ViewBag.InProgress = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.Processing);

            // 🌟 [CHANGED] เปลี่ยนจากรวมปริมาณน้ำมัน ➡️ นับจำนวนใบเสนอราคาที่ปิดงานสำเร็จ (Approved)
            // *หมายเหตุ: สามารถเปลี่ยนเป็น QuoteStatus.Completed หรือค่าอื่นตาม Enum จริงของคุณได้เลยครับ
            // 🌟 แก้เป็น QuoteStatus.QuotationSent ตัวแดงหายแน่นอนครับ!
            ViewBag.CompletedRequests = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.QuotationSent);

            // จำนวนแยกตามสถานะ ใช้แสดงตัวเลขบนปุ่มกรองสถานะด้านบนตาราง
            ViewBag.CountWon = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.Won);
            ViewBag.CountLost = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.Lost);

            // คำนวณคำขอที่ล่าช้า (> 24 ชม. และยังเป็นสถานะ New หรือ Processing)
            var overdueDeadline = DateTime.Now.AddHours(-24);
            ViewBag.OverdueRequests = await _context.Quotes.CountAsync(q =>
                (q.Status == QuoteStatus.New || q.Status == QuoteStatus.Processing) &&
                q.CreatedDate < overdueDeadline);

            // ==========================================
            // 3. เตรียมข้อมูลสำหรับกราฟ (Chart.js)
            // ==========================================

            // กราฟ 1: แนวโน้มคำขอ 7 วันล่าสุด
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-i))
                .Reverse()
                .ToList();

            var trendLabels = last7Days.Select(d => d.ToString("dd MMM")).ToList();

            // ⚡ [OPTIMIZED] ดึงเฉพาะข้อมูลกลุ่ม 7 วันย้อนหลังจาก DB มานับกรุ๊ป แทนการโหลดมาทั้งหมดใน Memory
            var startDate = DateTime.Today.AddDays(-6);
            var dailyCounts = await _context.Quotes
                .Where(q => q.CreatedDate.Date >= startDate)
                .GroupBy(q => q.CreatedDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var trendData = last7Days
                .Select(d => dailyCounts.FirstOrDefault(c => c.Date == d.Date)?.Count ?? 0)
                .ToList();

            ViewBag.TrendLabels = JsonSerializer.Serialize(trendLabels);
            ViewBag.TrendData = JsonSerializer.Serialize(trendData);

            // กราฟ 2: Top 5 สินค้ายอดฮิต
            var topProducts = await _context.QuoteDetails
                .GroupBy(d => d.ProductName)
                .Select(g => new {
                    ProductName = g.Key,
                    RequestCount = g.Count() // นับจำนวนครั้งที่ถูกขอ
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(5)
                .ToListAsync();

            var productLabels = topProducts.Select(p => p.ProductName).ToList();
            var productData = topProducts.Select(p => p.RequestCount).ToList();

            ViewBag.TopProductLabels = JsonSerializer.Serialize(productLabels);
            ViewBag.TopProductData = JsonSerializer.Serialize(productData);

            return View(quotes);
        }

        // GET: Quote/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var quote = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (quote == null) return NotFound();

            return View(quote);
        }

        // GET: Quote/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var quote = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null) return NotFound();

            return View(quote);
        }

        // POST: Quote/Edit/5
        // รับค่า Status, ชื่อผู้ขาย และราคาต่อหน่วยของแต่ละรายการสินค้าที่แอดมินกรอก
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Quote quote, int[] detailId, decimal[] detailPrice)
        {
            if (id != quote.Id) return NotFound();

            try
            {
                // ดึงข้อมูลเดิมจากฐานข้อมูลมาอัปเดต ป้องกันข้อมูลส่วนอื่นผิดเพี้ยน
                var existingQuote = await _context.Quotes
                    .Include(q => q.QuoteDetails)
                    .FirstOrDefaultAsync(q => q.Id == id);
                if (existingQuote == null) return NotFound();

                existingQuote.Status = quote.Status;
                existingQuote.SalesPersonName = quote.SalesPersonName;

                // อัปเดตราคาต่อหน่วยของแต่ละรายการสินค้าตามที่แอดมินกรอกเข้ามา
                if (detailId != null && detailPrice != null)
                {
                    for (int i = 0; i < detailId.Length && i < detailPrice.Length; i++)
                    {
                        var detail = existingQuote.QuoteDetails.FirstOrDefault(d => d.Id == detailId[i]);
                        if (detail != null)
                        {
                            detail.Price = detailPrice[i];
                        }
                    }
                }

                _context.Update(existingQuote);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "บันทึกราคาและสถานะเอกสารสำเร็จ";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuoteExists(quote.Id)) return NotFound();
                else throw;
            }
        }
        // ══════════════════════════════════════════════════════════════
        // เพิ่ม Action นี้เข้าไปใน QuoteController.cs เดิม
        // ══════════════════════════════════════════════════════════════

        // GET: /Quote/PrintQuotation/5
        [HttpGet]
        public async Task<IActionResult> PrintQuotation(int? id)
        {
            if (id == null) return NotFound();

            var quote = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null) return NotFound();

            // เลขที่ใบเสนอราคา format: QT{ปีพ.ศ.}{เดือน}{Id:D4} เช่น QT2026050031
            int buddhistYear = quote.CreatedDate.Year + 543;
            ViewBag.QuoteNumber = $"QT{buddhistYear}{quote.CreatedDate:MM}{quote.Id:D4}";

            decimal subtotal = quote.QuoteDetails.Sum(d => d.Price * (decimal)d.Quantity);
            decimal vat = Math.Round(subtotal * 0.07m, 2);
            decimal total = subtotal + vat;

            ViewBag.Subtotal = subtotal;
            ViewBag.Vat = vat;
            ViewBag.Total = total;
            ViewBag.TotalText = NumberToThaiText(total);

            return View(quote);
        }

        // POST: /Quote/SendQuotation/5
        // ส่งใบเสนอราคา (พร้อมราคาที่แอดมินกรอกไว้แล้ว) ไปยังอีเมลลูกค้า
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendQuotation(int id)
        {
            var quote = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote == null) return NotFound();

            if (string.IsNullOrEmpty(quote.Email))
            {
                TempData["ErrorMessage"] = "ไม่พบอีเมลลูกค้าสำหรับใบเสนอราคานี้";
                return RedirectToAction(nameof(Details), new { id });
            }

            int buddhistYear = quote.CreatedDate.Year + 543;
            string quoteNumber = $"QT{buddhistYear}{quote.CreatedDate:MM}{quote.Id:D4}";

            decimal subtotal = quote.QuoteDetails.Sum(d => d.Price * (decimal)d.Quantity);
            decimal vat = Math.Round(subtotal * 0.07m, 2);
            decimal total = subtotal + vat;

            string printUrl = Url.Action("PrintQuotation", "Quote", new { id = quote.Id }, Request.Scheme) ?? "";

            var rowsHtml = string.Join("", quote.QuoteDetails.Select(d => $@"
                <tr>
                    <td style='padding:8px;border-bottom:1px solid #edf2f7'>{d.ProductName}</td>
                    <td style='padding:8px;border-bottom:1px solid #edf2f7;text-align:right'>{d.Quantity:N0}</td>
                    <td style='padding:8px;border-bottom:1px solid #edf2f7;text-align:right'>{d.Price:N2}</td>
                    <td style='padding:8px;border-bottom:1px solid #edf2f7;text-align:right'>{(d.Price * (decimal)d.Quantity):N2}</td>
                </tr>"));

            string emailSubject = $"ใบเสนอราคา #{quoteNumber} จาก PPM ENERGY (THAILAND) CO.,LTD.";
            string emailBody = $@"
                <div style='font-family: sans-serif; padding: 20px; max-width: 640px; border: 1px solid #e2e8f0; border-radius: 16px; background-color: #F8F8FF;'>
                    <h2 style='color: #009933; margin-bottom: 10px; font-size: 22px;'>ใบเสนอราคา #{quoteNumber}</h2>
                    <p style='color:#4a5568'>เรียนคุณ {quote.ContactName},<br>ทางบริษัท PPM ENERGY (THAILAND) CO.,LTD. ขอนำส่งใบเสนอราคาตามรายละเอียดด้านล่างนี้</p>
                    <table style='width:100%;border-collapse:collapse;margin-top:12px'>
                        <thead>
                            <tr style='background:#009933;color:white'>
                                <th style='padding:8px;text-align:left'>รายการ</th>
                                <th style='padding:8px;text-align:right'>จำนวน</th>
                                <th style='padding:8px;text-align:right'>ราคา/หน่วย</th>
                                <th style='padding:8px;text-align:right'>รวม</th>
                            </tr>
                        </thead>
                        <tbody>{rowsHtml}</tbody>
                    </table>
                    <table style='width:100%;margin-top:12px'>
                        <tr><td style='text-align:right;padding:4px;color:#4a5568'>รวมเป็นเงิน</td><td style='text-align:right;padding:4px;width:120px'>{subtotal:N2} บาท</td></tr>
                        <tr><td style='text-align:right;padding:4px;color:#dc2626'>ภาษีมูลค่าเพิ่ม 7%</td><td style='text-align:right;padding:4px;color:#dc2626'>{vat:N2} บาท</td></tr>
                        <tr><td style='text-align:right;padding:6px;font-weight:bold;color:#009933'>ยอดรวมทั้งสิ้น</td><td style='text-align:right;padding:6px;font-weight:bold;color:#009933'>{total:N2} บาท</td></tr>
                    </table>
                    <p style='margin-top:20px'><a href='{printUrl}' style='background:#009933;color:white;padding:10px 20px;border-radius:8px;text-decoration:none;font-weight:bold'>ดู / พิมพ์ใบเสนอราคาฉบับเต็ม</a></p>
                    <hr style='border:0;border-top:1px solid #e2e8f0;margin:20px 0' />
                    <p style='font-size:12px;color:#a0aec0;text-align:center;margin:0'>PPM ENERGY (THAILAND) CO.,LTD. | โทร. 035-245-899</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(quote.Email, emailSubject, emailBody);

                quote.Status = QuoteStatus.QuotationSent;
                _context.Update(quote);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"ส่งใบเสนอราคาไปยัง {quote.Email} สำเร็จ";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "ส่งอีเมลไม่สำเร็จ: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Helper: แปลงตัวเลขเป็นตัวอักษรภาษาไทย ──
        private static string NumberToThaiText(decimal amount)
        {
            if (amount == 0) return "ศูนย์บาทถ้วน";
            string[] ones = { "", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };
            long baht = (long)Math.Floor(amount);
            int satang = (int)Math.Round((amount - baht) * 100);
            string result = ConvertLong(baht, ones) + "บาท";
            result += satang > 0 ? ConvertLong(satang, ones) + "สตางค์" : "ถ้วน";
            return result;
        }
        private static string ConvertLong(long n, string[] ones)
        {
            if (n == 0) return "";
            if (n >= 1_000_000) return ConvertLong(n / 1_000_000, ones) + "ล้าน" + ConvertLong(n % 1_000_000, ones);
            if (n >= 100_000) return ones[n / 100_000] + "แสน" + ConvertLong(n % 100_000, ones);
            if (n >= 10_000) return ones[n / 10_000] + "หมื่น" + ConvertLong(n % 10_000, ones);
            if (n >= 1_000) return ones[n / 1_000] + "พัน" + ConvertLong(n % 1_000, ones);
            if (n >= 100) return ones[n / 100] + "ร้อย" + ConvertLong(n % 100, ones);
            if (n >= 20) return new[] { "", "", "ยี่สิบ", "สามสิบ", "สี่สิบ", "ห้าสิบ", "หกสิบ", "เจ็ดสิบ", "แปดสิบ", "เก้าสิบ" }[n / 10]
                                       + (n % 10 > 0 ? ones[n % 10] : "");
            if (n == 10) return "สิบ";
            if (n == 11) return "สิบเอ็ด";
            if (n <= 19) return "สิบ" + ones[n % 10];
            return ones[n];
        }

        // GET: Quote/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var quote = await _context.Quotes
                .FirstOrDefaultAsync(m => m.Id == id);

            if (quote == null) return NotFound();

            return View(quote);
        }

        // POST: Quote/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var quote = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quote != null)
            {
                _context.QuoteDetails.RemoveRange(quote.QuoteDetails);
                _context.Quotes.Remove(quote);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "ลบข้อมูลคำขอใบเสนอราคาสำเร็จ";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // ส่วนของ Customer (หน้าบ้าน)
        // ==========================================

        // POST: /Quote/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Quote model, int productId, double qty)
        {
            ModelState.Remove("QuoteDetails");

            if (ModelState.IsValid)
            {
                try
                {
                    var product = await _context.Products.FindAsync(productId);
                    string productName = product != null ? product.Name : "สินค้าไม่ทราบชื่อ";

                    model.CreatedDate = DateTime.Now;
                    model.Status = QuoteStatus.New;

                    // 📍 [NEW] ตรวจสอบการล็อกอิน และดึง UserId มาบันทึกลงไป
                    if (User.Identity != null && User.Identity.IsAuthenticated)
                    {
                        model.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    }

                    model.QuoteDetails = new System.Collections.Generic.List<QuoteDetail>
                    {
                        new QuoteDetail
                        {
                            ProductId = productId,
                            ProductName = productName,
                            Quantity = qty
                        }
                    };

                    _context.Quotes.Add(model);
                    await _context.SaveChangesAsync();

                    // แจ้งเตือนผ่านระบบ SignalR เดิมของคุณ
                    await _hubContext.Clients.All.SendAsync("ReceiveNewQuote", model.CompanyName, productName);

                    // ========================================================
                    // แทรกระบบส่งอีเมลแจ้งเตือนเข้าเมลแอดมินบริษัทตรงนี้
                    // ========================================================
                    try
                    {
                        var adminEmail = _configuration["EmailSettings:AdminEmail"];
                        string emailSubject = $"[ใบเสนอราคาใหม่] จาก {model.CompanyName} - ผู้ติดต่อ: {model.ContactName}";

                        string emailBody = $@"
                            <div style='font-family: sans-serif; padding: 20px; max-width: 600px; border: 1px solid #e2e8f0; border-radius: 16px; background-color: #F8F8FF;'>
                                <h2 style='color: #009933; margin-bottom: 20px; font-size: 22px;'>🔔 มีคำขอใบเสนอราคาใหม่เข้าระบบ</h2>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; width: 35%; border-bottom: 1px solid #edf2f7;'>บริษัท / องค์กร:</td>
                                        <td style='padding: 10px 0; color: #1a202c; border-bottom: 1px solid #edf2f7;'>{model.CompanyName}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; border-bottom: 1px solid #edf2f7;'>ชื่อผู้ติดต่อ:</td>
                                        <td style='padding: 10px 0; color: #1a202c; border-bottom: 1px solid #edf2f7;'>{model.ContactName}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; border-bottom: 1px solid #edf2f7;'>อีเมลลูกค้า:</td>
                                        <td style='padding: 10px 0; color: #1a202c; border-bottom: 1px solid #edf2f7;'><a href='mailto:{model.Email}' style='color: #009933;'>{model.Email}</a></td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; border-bottom: 1px solid #edf2f7;'>เบอร์โทรศัพท์:</td>
                                        <td style='padding: 10px 0; color: #1a202c; border-bottom: 1px solid #edf2f7;'>{model.Phone}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; border-bottom: 1px solid #edf2f7;'>สินค้าที่สนใจ:</td>
                                        <td style='padding: 10px 0; color: #009933; font-weight: bold; border-bottom: 1px solid #edf2f7;'>{productName} (จำนวน {qty} รายการ)</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568; border-bottom: 1px solid #edf2f7;'>สถานที่จัดส่ง:</td>
                                        <td style='padding: 10px 0; color: #1a202c; border-bottom: 1px solid #edf2f7;'>{model.DeliveryLocation}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 0; font-weight: bold; color: #4a5568;'>หมายเหตุ:</td>
                                        <td style='padding: 10px 0; color: #718096; font-style: italic;'>{(string.IsNullOrEmpty(model.Note) ? "-" : model.Note)}</td>
                                    </tr>
                                </table>
                                <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                                <p style='font-size: 12px; color: #a0aec0; text-align: center; margin: 0;'>ระบบนี้เป็นการแจ้งเตือนอัตโนมัติจากเว็บไซต์ PPMEnergyWeb</p>
                            </div>";

                        await _emailService.SendEmailAsync(adminEmail, emailSubject, emailBody);
                    }
                    catch (Exception mailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Email Error] ส่งเมลไม่สำเร็จ: {mailEx.Message}");
                    }
                    // ========================================================

                    TempData["SuccessMessage"] = "ส่งข้อมูลขอใบเสนอราคาสำเร็จ! ทีมงานจะรีบติดต่อกลับโดยเร็วที่สุด";
                    return RedirectToAction("Details", "Products", new { id = productId });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "เกิดข้อผิดพลาดในการบันทึกข้อมูล: " + ex.Message;
                    return RedirectToAction("Details", "Products", new { id = productId });
                }
            }

            TempData["ErrorMessage"] = "กรุณากรอกข้อมูลที่จำเป็นให้ครบถ้วน";
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        private bool QuoteExists(int id)
        {
            return _context.Quotes.Any(e => e.Id == id);
        }
    }
}