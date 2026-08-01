using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using System.Security.Claims;
using PPMEnergyWeb.Models;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1.3.1.1: หน้าจัดการโปรไฟล์และที่อยู่จัดส่ง
        // ==========================================
        public async Task<IActionResult> Profile()
        {
            // 📍 [ปรับปรุง] เปลี่ยนจาก ID: 1 (Hardcode) เป็นการดึง Id ของผู้ใช้ที่ล็อกอินอยู่จริงผ่าน Claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // 📍 [ปรับปรุง] เปลี่ยนพารามิเตอร์ id จาก int เป็น string เพื่อให้ตรงกับมาตรฐาน Identity User Id
        public async Task<IActionResult> UpdateAddress(string id, string shippingAddress)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.ShippingAddress = shippingAddress;
                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "อัปเดตที่อยู่จัดส่งเรียบร้อยแล้ว";
            }
            return RedirectToAction(nameof(Profile));
        }

        // ==========================================
        // 1.3.1.3: หน้าติดตามสถานะใบเสนอราคาและการสั่งซื้อ
        // ==========================================
        public async Task<IActionResult> TrackQuotes()
        {
            // 1. ดึงข้อมูลตัวตนของคนที่ล็อกอินอยู่ (ดึงทั้ง UserId และ Email)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            // ป้องกันไว้ก่อน: ถ้าไม่มีคนล็อกอิน ให้เด้งกลับไปหน้า Login
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // 2. ดึงข้อมูลใบเสนอราคา
            // 📍 [ปรับปรุง] ค้นหาโดยใช้เงื่อนไข " UserId ตรงกัน" หรือ "Email ตรงกัน" ควบคู่กันไป
            // วิธีนี้จะทำให้ "ข้อมูลเก่า" (ที่เคยเป็น Guest/UserId เป็น null) และ "ข้อมูลใหม่" (ที่มี UserId แล้ว) แสดงผลขึ้นมาครบถ้วนทั้งหมดครับ
            var quotes = await _context.Quotes
                .Include(q => q.QuoteDetails)
                .Where(q => q.UserId == userId || q.Email == userEmail)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();

            return View(quotes);
        }
    }
}