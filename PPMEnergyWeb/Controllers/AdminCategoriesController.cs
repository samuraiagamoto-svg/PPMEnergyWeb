using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;

namespace PPMEnergyWeb.Controllers
{
    public class AdminCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─── Helper: โหลด dropdown หมวดหมู่หลักทั้งหมด (ParentCategoryId == null) ───
        // excludeId = ป้องกันไม่ให้ category เลือกตัวเองเป็น parent
        private async Task LoadParentCategoriesDropdown(int? excludeId = null)
        {
            var mainCategories = await _context.Categories
                .Where(c => c.ParentCategoryId == null && c.Id != excludeId)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            ViewBag.ParentCategories = new SelectList(mainCategories, "Id", "DisplayName");
        }

        // 1. หน้าแสดงรายการหมวดหมู่ทั้งหมด (รวม Sub Categories)
        public async Task<IActionResult> Index()
        {
            // Include ParentCategory เพื่อดึงชื่อ parent มาแสดงใน Index
            var categories = await _context.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategoryId == null ? 0 : 1)   // Main ก่อน
                .ThenBy(c => c.ParentCategoryId)
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();

            return View(categories);
        }

        // 2. หน้าเพิ่มหมวดหมู่ใหม่ (GET)
        public async Task<IActionResult> Create()
        {
            await LoadParentCategoriesDropdown();
            return View();
        }

        // 3. บันทึกหมวดหมู่ใหม่ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                category.CategoryCode = category.CategoryCode.Trim().ToLower();
                category.DisplayName = category.DisplayName.Trim();

                // ─── 🛠️ ส่วนจัดการอัปโหลดไฟล์รูปภาพใหม่ ───
                if (category.ImageFile != null && category.ImageFile.Length > 0)
                {
                    string folder = "images/categories/";
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(category.ImageFile.FileName);
                    string serverFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder);

                    // สร้างโฟลเดอร์กรณีที่ยังไม่มีในโปรเจกต์
                    if (!Directory.Exists(serverFolder))
                    {
                        Directory.CreateDirectory(serverFolder);
                    }

                    string filePath = Path.Combine(serverFolder, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await category.ImageFile.CopyToAsync(fileStream);
                    }

                    // บันทึก Path รูปภาพลงในคอลัมน์ ImageUrl
                    category.ImageUrl = "/" + folder + fileName;
                }
                // ────────────────────────────────────────

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadParentCategoriesDropdown();
            return View(category);
        }

        // 4. หน้าแก้ไขหมวดหมู่ (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            // ส่ง dropdown โดย exclude ตัวเองออก (ป้องกัน circular reference)
            await LoadParentCategoriesDropdown(excludeId: id);
            return View(category);
        }

        // 5. บันทึกการแก้ไขหมวดหมู่ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id) return NotFound();

            // ป้องกัน circular: ห้ามเลือกตัวเองเป็น parent
            if (category.ParentCategoryId == id)
            {
                ModelState.AddModelError("ParentCategoryId", "ไม่สามารถเลือกตัวเองเป็นหมวดหมู่หลักได้");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    category.CategoryCode = category.CategoryCode.Trim().ToLower();
                    category.DisplayName = category.DisplayName.Trim();

                    // ─── 🛠️ ส่วนจัดการอัปโหลดภาพกรณีแก้ไขข้อมูล ───
                    if (category.ImageFile != null && category.ImageFile.Length > 0)
                    {
                        string folder = "images/categories/";
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(category.ImageFile.FileName);
                        string serverFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder);

                        if (!Directory.Exists(serverFolder))
                        {
                            Directory.CreateDirectory(serverFolder);
                        }

                        // [ตัวเลือกเสริมที่ดี] ลบไฟล์รูปเก่าออกจากเซิร์ฟเวอร์ก่อน เพื่อไม่ให้ขยะเต็มโฮสต์
                        if (!string.IsNullOrEmpty(category.ImageUrl))
                        {
                            string oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", category.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        string filePath = Path.Combine(serverFolder, fileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await category.ImageFile.CopyToAsync(fileStream);
                        }

                        // อัปเดต Path รูปใหม่เข้าไปแทนที่
                        category.ImageUrl = "/" + folder + fileName;
                    }
                    // หมายเหตุ: หากผู้ใช้ไม่ได้เลือกรูปใหม่ (category.ImageFile == null) 
                    // ตัวฟอร์ม Edit.cshtml จะส่งค่า ImageUrl ตัวเดิมกลับมาให้ผ่าน <input type="hidden"> ทำให้รูปเดิมไม่หายไปไหนครับ
                    // ────────────────────────────────────────

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.Id == category.Id)) return NotFound();
                    else throw;
                }

                return RedirectToAction(nameof(Index));
            }

            await LoadParentCategoriesDropdown(excludeId: id);
            return View(category);
        }

        // 6. ลบหมวดหมู่สินค้า
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // 1. ดึงแค่ตัว Category หลักออกมาพอ (ไม่ต้อง Include เพราะเราตั้ง [NotMapped] ไว้)
            var category = await _context.Categories.FindAsync(id);

            if (category != null)
            {
                // [ตัวเลือกเสริมที่ดี] ลบรูปภาพออกจากระบบปฏิบัติการเมื่อลบหมวดหมู่นั้นทิ้ง
                if (!string.IsNullOrEmpty(category.ImageUrl))
                {
                    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", category.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // 2. ดึงหมวดย่อย (SubCategories) ออกมาแยกต่างหากด้วยตัวเอง
                var subCategories = await _context.Categories
                    .Where(c => c.ParentCategoryId == id)
                    .ToListAsync();

                // 3. ถ้ามีหมวดย่อย ให้จัดการ Reset ParentCategoryId
                foreach (var sub in subCategories)
                {
                    sub.ParentCategoryId = null;
                }

                // 4. ลบ Category หลัก
                _context.Categories.Remove(category);

                // 5. SaveChanges ครั้งเดียว ระบบจะจัดการอัปเดตสถานะ sub และลบ parent ให้เรียบร้อย
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}