using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Text;

namespace PPMEnergyWeb.Controllers
{
    [Authorize(Roles = "Owner")]
    public class OwnerReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OwnerReportController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ── Dashboard ────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var totalUsers   = _userManager.Users.Count();
            var newThisMonth = _userManager.Users
                .Where(u => u.CreatedDate.Month == DateTime.Now.Month &&
                            u.CreatedDate.Year  == DateTime.Now.Year)
                .Count();

            var totalQuotes   = await _context.Quotes.CountAsync();
            var pendingQuotes = await _context.Quotes
                .Where(q => q.Status == QuoteStatus.New).CountAsync();

            // ใบเสนอราคารายเดือน (6 เดือนล่าสุด)
            var sixMonthsAgo = DateTime.Now.AddMonths(-5);
            var monthlyQuotes = await _context.Quotes
                .Where(q => q.CreatedDate >= sixMonthsAgo)
                .GroupBy(q => new { q.CreatedDate.Year, q.CreatedDate.Month })
                .Select(g => new {
                    Year  = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            ViewBag.TotalUsers    = totalUsers;
            ViewBag.NewThisMonth  = newThisMonth;
            ViewBag.TotalQuotes   = totalQuotes;
            ViewBag.PendingQuotes = pendingQuotes;
            ViewBag.MonthlyQuotes = monthlyQuotes;

            return View();
        }

        // ── รายงานลูกค้า ─────────────────────────────────────────
        public async Task<IActionResult> Customers(string? search, int page = 1)
        {
            const int pageSize = 20;

            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                usersQuery = usersQuery.Where(u =>
                    u.Email!.Contains(search) ||
                    u.UserName!.Contains(search));

            var totalCount = usersQuery.Count();
            var users = usersQuery
                .OrderByDescending(u => u.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // นับใบเสนอราคาของแต่ละ user
            var userIds    = users.Select(u => u.Id).ToList();
            var quoteCounts = await _context.Quotes
                .Where(q => userIds.Contains(q.UserId ?? ""))
                .GroupBy(q => q.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId!, g => g.Count);

            ViewBag.Search      = search;
            ViewBag.Page        = page;
            ViewBag.TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount  = totalCount;
            ViewBag.QuoteCounts = quoteCounts;

            return View(users);
        }

        // ── รายงานใบเสนอราคา ─────────────────────────────────────
        public async Task<IActionResult> Quotes(
            string? search,
            string? status,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Quotes
                .Include(q => q.QuoteDetails)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(q =>
                    q.CompanyName.Contains(search) ||
                    q.ContactName.Contains(search) ||
                    q.Email.Contains(search));

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<QuoteStatus>(status, out var statusEnum))
                query = query.Where(q => q.Status == statusEnum);

            if (dateFrom.HasValue)
                query = query.Where(q => q.CreatedDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(q => q.CreatedDate <= dateTo.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var quotes = await query
                .OrderByDescending(q => q.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search     = search;
            ViewBag.Status     = status;
            ViewBag.DateFrom   = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo     = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(quotes);
        }

        // ── Export Excel ใบเสนอราคา ───────────────────────────────
        public async Task<IActionResult> ExportQuotesExcel(
            string? search,
            string? status,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var query = _context.Quotes
                .Include(q => q.QuoteDetails)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(q =>
                    q.CompanyName.Contains(search) ||
                    q.ContactName.Contains(search) ||
                    q.Email.Contains(search));

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<QuoteStatus>(status, out var statusEnum))
                query = query.Where(q => q.Status == statusEnum);

            if (dateFrom.HasValue)
                query = query.Where(q => q.CreatedDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(q => q.CreatedDate <= dateTo.Value.AddDays(1));

            var quotes = await query.OrderByDescending(q => q.CreatedDate).ToListAsync();

            // สร้าง CSV (เปิดได้ใน Excel)
            var sb = new StringBuilder();
            sb.AppendLine("\uFEFF" + // BOM สำหรับ UTF-8 ให้ภาษาไทยแสดงได้
                "ลำดับ,วันที่,บริษัท,ผู้ติดต่อ,อีเมล,เบอร์โทร,สถานที่จัดส่ง,หมายเหตุ,สถานะ,จำนวนรายการ");

            int i = 1;
            foreach (var q in quotes)
            {
                sb.AppendLine(string.Join(",",
                    i++,
                    q.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                    $"\"{q.CompanyName}\"",
                    $"\"{q.ContactName}\"",
                    q.Email,
                    q.Phone,
                    $"\"{q.DeliveryLocation}\"",
                    $"\"{q.Note?.Replace("\"", "'")}\"",
                    q.Status.ToString(),
                    q.QuoteDetails.Count
                ));
            }

            var fileName = $"QuoteReport_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName);
        }

        // ── Export Excel ลูกค้า ───────────────────────────────────
        public async Task<IActionResult> ExportCustomersExcel()
        {
            var users = _userManager.Users
                .OrderByDescending(u => u.CreatedDate)
                .ToList();

            var quoteCounts = await _context.Quotes
                .GroupBy(q => q.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId!, g => g.Count);

            var sb = new StringBuilder();
            sb.AppendLine("\uFEFF" +
                "ลำดับ,อีเมล,ชื่อผู้ใช้,วันที่สมัคร,จำนวนใบเสนอราคา");

            int i = 1;
            foreach (var u in users)
            {
                quoteCounts.TryGetValue(u.Id, out int count);
                sb.AppendLine(string.Join(",",
                    i++,
                    u.Email,
                    u.UserName,
                    u.CreatedDate.ToString("dd/MM/yyyy"),
                    count
                ));
            }

            var fileName = $"CustomerReport_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName);
        }
    }
}
