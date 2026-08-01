using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Drawing;

namespace PPMEnergyWeb.Controllers
{
    [Authorize(Roles = "9")]
    public class AdminCustomersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminCustomersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // 1. หน้าแสดงรายชื่อลูกค้า + ค้นหา/กรองประเภทลูกค้า
        public async Task<IActionResult> Index(string? search, string? sortBy, string? customerType, int page = 1)
        {
            int pageSize = 20;

            // ดึง users ทั้งหมดที่ไม่ใช่ role "9" (Admin)
            var adminIds = (await _userManager.GetUsersInRoleAsync("9"))
                           .Select(u => u.Id).ToHashSet();

            var query = _userManager.Users
                .Where(u => !adminIds.Contains(u.Id));

            // ── คำนวณยอดรวมของแต่ละประเภท (สำหรับไปโชว์ที่ป้ายตัวเลข/แท็บหน้า UI) ──
            ViewBag.CountAll = await query.CountAsync();
            ViewBag.CountHasQuote = await query.Where(u => _context.Quotes.Any(q => q.UserId == u.Id)).CountAsync();
            ViewBag.CountNoQuote = await query.Where(u => !_context.Quotes.Any(q => q.UserId == u.Id)).CountAsync();

            // ── คัดกรองตามประเภทลูกค้า (แบ่งตามประวัติใบเสนอราคา) ──────────────────
            if (customerType == "hasQuote")
            {
                query = query.Where(u => _context.Quotes.Any(q => q.UserId == u.Id));
            }
            else if (customerType == "noQuote")
            {
                query = query.Where(u => !_context.Quotes.Any(q => q.UserId == u.Id));
            }

            // ── ค้นหา ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(search)) ||
                    (u.Email != null && u.Email.ToLower().Contains(search)) ||
                    (u.CompanyName != null && u.CompanyName.ToLower().Contains(search)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            // ── เรียงลำดับ ────────────────────────────────────────
            query = sortBy switch
            {
                "email" => query.OrderBy(u => u.Email),
                "phone" => query.OrderBy(u => u.PhoneNumber),
                _ => query.OrderBy(u => u.UserName)
            };

            int total = await query.CountAsync();

            var customers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.SortBy = sortBy;
            ViewBag.CustomerType = customerType; // ส่งค่าประเภทที่เลือกกลับไปให้หน้า View ทำ Active Tab
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View(customers);
        }

        // 2. หน้ารายละเอียดลูกค้า (ดู Quote ด้วย)
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var quotes = await _context.Quotes
                .Where(q => q.UserId == id)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();

            ViewBag.Quotes = quotes;
            return View(user);
        }

        // 3. Export Excel (ปรับปรุงให้กรองประเภทลูกค้าตามหน้าเว็บได้ด้วย)
        public async Task<IActionResult> ExportExcel(string? search, string? customerType)
        {
            var adminIds = (await _userManager.GetUsersInRoleAsync("9"))
                           .Select(u => u.Id).ToHashSet();

            var query = _userManager.Users
                .Where(u => !adminIds.Contains(u.Id));

            // กรองประเภทข้อมูลให้ตรงกับที่แอดมินเลือกอยู่ตอนนั้นก่อนจะกด Export
            if (customerType == "hasQuote")
            {
                query = query.Where(u => _context.Quotes.Any(q => q.UserId == u.Id));
            }
            else if (customerType == "noQuote")
            {
                query = query.Where(u => !_context.Quotes.Any(q => q.UserId == u.Id));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(search)) ||
                    (u.Email != null && u.Email.ToLower().Contains(search)) ||
                    (u.CompanyName != null && u.CompanyName.ToLower().Contains(search)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            var customers = await query.OrderBy(u => u.UserName).ToListAsync();

            using var wb = new XLWorkbook();
            var sheetName = customerType switch
            {
                "hasQuote" => "ลูกค้าที่เคยขอใบเสนอราคา",
                "noQuote" => "ลูกค้าที่ยังไม่เคยขอใบเสนอราคา",
                _ => "รายชื่อลูกค้าทั้งหมด"
            };
            var ws = wb.Worksheets.Add(sheetName);

            // Header
            var headers = new[] { "ลำดับ", "ชื่อผู้ใช้", "บริษัท", "อีเมล", "เบอร์โทร", "ที่อยู่จัดส่ง", "ยืนยันอีเมล" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#16a34a");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            for (int i = 0; i < customers.Count; i++)
            {
                var c = customers[i];
                ws.Cell(i + 2, 1).Value = i + 1;
                ws.Cell(i + 2, 2).Value = c.UserName ?? "-";
                ws.Cell(i + 2, 3).Value = c.CompanyName ?? "-";
                ws.Cell(i + 2, 4).Value = c.Email ?? "-";
                ws.Cell(i + 2, 5).Value = c.PhoneNumber ?? "-";
                ws.Cell(i + 2, 6).Value = c.ShippingAddress ?? "-";
                ws.Cell(i + 2, 7).Value = c.EmailConfirmed ? "ยืนยันแล้ว" : "ยังไม่ยืนยัน";

                if (i % 2 == 1)
                    ws.Row(i + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            string filePrefix = customerType ?? "all";
            string fileName = $"customers_{filePrefix}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // 4. ลบลูกค้า
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
                await _userManager.DeleteAsync(user);

            return RedirectToAction(nameof(Index));
        }
    }
}