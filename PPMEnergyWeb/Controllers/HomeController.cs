using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public HomeController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // หน้าแรกสำหรับทุกคน (โชว์สินค้า 4 รายการ + ข้อมูลจาก Config)
        public async Task<IActionResult> Index()
        {
            // 1. ดึงข้อมูลการตั้งค่าหน้าแรก (ถ้าไม่มีให้สร้าง Default)
            var config = await _context.HomeConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new HomeConfig { HeroTitle = "Welcome to PPM Energy", HeroDescription = "ผู้จำหน่ายน้ำมันเตาคุณภาพ" };
            }

            // 2. ดึงสินค้าล่าสุด 4 รายการ
            var featuredProducts = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id)
                .Take(4)
                .ToListAsync();

            // ส่งข้อมูลไปที่ View
            ViewBag.HomeConfig = config;
            return View(featuredProducts);
        }

        // --- ส่วนจัดการหน้า Home (เฉพาะ Admin Role 9 เท่านั้น) ---
        [Authorize(Roles = "9")]
        public async Task<IActionResult> HomeManagement()
        {
            var config = await _context.HomeConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new HomeConfig { HeroTitle = "PPM Energy Web", HeroDescription = "รายละเอียดหน้าแรกของคุณ" };
                _context.HomeConfigs.Add(config);
                await _context.SaveChangesAsync();
            }
            return View(config);
        }

        [HttpPost]
        [Authorize(Roles = "9")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateHome(HomeConfig model, IFormFile? bannerFile)
        {
            var config = await _context.HomeConfigs.FirstOrDefaultAsync(p => p.Id == model.Id);
            if (config == null) return NotFound();

            if (ModelState.IsValid)
            {
                config.HeroTitle = model.HeroTitle;
                config.HeroDescription = model.HeroDescription;
                config.ContactPhone = model.ContactPhone;

                // จัดการอัปโหลดรูปแบนเนอร์ใหม่
                if (bannerFile != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(bannerFile.FileName);
                    string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");

                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string path = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await bannerFile.CopyToAsync(stream);
                    }
                    config.BannerImageUrl = "/uploads/" + fileName;
                }

                _context.Update(config);
                await _context.SaveChangesAsync();
                TempData["Success"] = "อัปเดตข้อมูลหน้าแรกเรียบร้อยแล้ว!";
                return RedirectToAction(nameof(HomeManagement));
            }
            return View("HomeManagement", model);
        }

        public IActionResult Services()
        {
            return View();
        }

        // 🟢 เพิ่ม Action สำหรับหน้าเกี่ยวกับบริษัท (About Us)
        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }
    }
}