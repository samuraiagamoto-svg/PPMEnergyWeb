using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;

namespace PPMEnergyWeb.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. หน้าแสดงรายการหมวดหมู่
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            return View("~/Views/AdminCategories/Index.cshtml", categories);
        }

        // 2. หน้าสำหรับสร้าง (GET) - แก้ไขพาธตรงนี้แล้ว
        public IActionResult Create()
        {
            return View("~/Views/AdminCategories/Create.cshtml");
        }

        // 3. รับค่าสร้าง (POST) - แก้ไขกรณี Error ให้ส่ง Model กลับไปด้วย
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/AdminCategories/Create.cshtml", category);
        }

        // 4. หน้าสำหรับแก้ไข (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View("~/Views/AdminCategories/Edit.cshtml", category);
        }

        // 5. รับค่าแก้ไข (POST) - แก้ไขพาธบรรทัดสุดท้ายให้ถูกต้องเวลาแก้ไขไม่ผ่าน
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/AdminCategories/Edit.cshtml", category);
        }

        // 6. ลบหมวดหมู่ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}