using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Text.Json; // ⚠️ เพิ่มเพื่อใช้แปลงข้อมูลไปทำกราฟฝั่ง View

namespace PPMEnergyWeb.Controllers
{
    [Authorize(Roles = "9")]
    public class AdminProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public AdminProductsController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // ── helper: โหลด categories สำหรับ Dropdown แบบ Grouped ──────────────
        private async Task LoadCategoryDropdown(int? selectedId = null)
        {
            var mains = await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var subs = await _context.Categories
                .Where(c => c.ParentCategoryId != null)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var groupedItems = new List<SelectListItem>();

            foreach (var main in mains)
            {
                var children = subs.Where(s => s.ParentCategoryId == main.Id).ToList();

                if (children.Any())
                {
                    var group = new SelectListGroup { Name = $"📁 {main.DisplayName}" };
                    foreach (var sub in children)
                    {
                        groupedItems.Add(new SelectListItem
                        {
                            Value = sub.Id.ToString(),
                            Text = sub.DisplayName,
                            Selected = selectedId.HasValue && sub.Id == selectedId,
                            Group = group
                        });
                    }
                }
                else
                {
                    groupedItems.Add(new SelectListItem
                    {
                        Value = main.Id.ToString(),
                        Text = $"📁 {main.DisplayName}",
                        Selected = selectedId.HasValue && main.Id == selectedId
                    });
                }
            }

            ViewBag.CategoryId = groupedItems;
        }

        // 1. เพิ่มพารามิเตอร์ searchString เข้ามารับค่าคำค้นหา
        public async Task<IActionResult> Index(string searchString, int page = 1)
        {
            int pageSize = 5;

            // 2. สร้าง Query เริ่มต้น (ใช้ AsQueryable เพื่อเตรียมประกอบคำสั่ง โดยยังไม่ดึงข้อมูลทั้งหมดลง Memory)
            var query = _context.Products
                .Include(p => p.Attributes)
                .Include(p => p.Category)
                    .ThenInclude(c => c!.ParentCategory)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // ค้นหาทั้งจาก ชื่อสินค้า, รายละเอียด และ ชื่อหมวดหมู่
                query = query.Where(p => p.Name.Contains(searchString) ||
                                         p.Description.Contains(searchString) ||
                                         (p.Category != null && p.Category.DisplayName.Contains(searchString)));
            }

            // 4. ส่งค่าคำค้นหากลับไปให้ View เพื่อให้ช่องค้นหาไม่ว่างเปล่าตอนโหลดหน้าใหม่ และใช้ต่อยอดในปุ่มเปลี่ยนหน้า
            ViewBag.CurrentSearch = searchString;

            // ดึงข้อมูล "ที่ผ่านการค้นหาแล้ว" (หรือทั้งหมดถ้าไม่ได้ค้น) มาใช้คำนวณ KPI และกราฟ
            var products = await query.ToListAsync();

            // ==========================================
            // 📊 1. แถบตัวเลขสรุปภาพรวม (KPI Cards)
            // ==========================================
            ViewBag.TotalProducts = products.Count;

            ViewBag.TotalCategories = await _context.Categories.CountAsync(c => c.ParentCategoryId == null);

            // ==========================================
            // ⚠️ 2. ส่วนแจ้งเตือนการจัดการ (Admin Alerts)
            // ==========================================
            ViewBag.IncompleteProducts = products.Count(p =>
                string.IsNullOrEmpty(p.ImageUrl) || p.CategoryId == null || p.CategoryId == 0);

            // ==========================================
            // 📈 3. กราฟสรุปสัดส่วนสินค้าตามหมวดหมู่ (Pie/Doughnut Chart)
            // ==========================================
            // 🔹 ของเดิมของคุณ (ยังเก็บไว้)
            var categoryGroup = products
                .Where(p => p.Category != null)
                .GroupBy(p => p.Category!.DisplayName ?? "ไม่ระบุหมวดหมู่")
                .Select(g => new { CategoryName = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.CategoryLabels = JsonSerializer.Serialize(categoryGroup.Select(c => c.CategoryName));
            ViewBag.CategoryData = JsonSerializer.Serialize(categoryGroup.Select(c => c.Count));

            // ➕ [เพิ่มใหม่]: สำหรับทำกราฟ Doughnut 2 ชั้น (หมวดหมู่หลัก vs ย่อย)
            var mainCatGroup = products
                .GroupBy(p => p.Category?.ParentCategory?.DisplayName ?? p.Category?.DisplayName ?? "อื่นๆ")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            var subCatGroup = products
                .GroupBy(p => p.Category?.DisplayName ?? "ไม่ระบุหมวดหมู่")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.MainCatLabels = JsonSerializer.Serialize(mainCatGroup.Select(x => x.Name));
            ViewBag.MainCatData = JsonSerializer.Serialize(mainCatGroup.Select(x => x.Count));
            ViewBag.SubCatLabels = JsonSerializer.Serialize(subCatGroup.Select(x => x.Name));
            ViewBag.SubCatData = JsonSerializer.Serialize(subCatGroup.Select(x => x.Count));

            // ==========================================
            // 🔥 4. กราฟ Top 5 สินค้ายอดฮิต (ดึงข้ามมาจากตารางใบเสนอราคา)
            // ==========================================
            var topProducts = await _context.QuoteDetails
                .GroupBy(d => d.ProductName)
                .Select(g => new {
                    ProductName = g.Key,
                    RequestCount = g.Count()
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(5)
                .ToListAsync();

            ViewBag.TopProductLabels = JsonSerializer.Serialize(topProducts.Select(p => p.ProductName));
            ViewBag.TopProductData = JsonSerializer.Serialize(topProducts.Select(p => p.RequestCount));

            // ==========================================
            // ⚙️ 5. ส่วนคำนวณการแบ่งหน้า (Pagination Logic)
            // ==========================================
            int totalItems = products.Count; // ตอนนี้จะนับเฉพาะจำนวนที่ Search เจอ (หรือทั้งหมดถ้าไม่ได้ Search)
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;

            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var pagedProducts = products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(pagedProducts);
        }

        // 2. หน้าสร้างสินค้า (GET)
        public async Task<IActionResult> Create()
        {
            await LoadCategoryDropdown();
            return View(new Product());
        }

        // 3. บันทึกสินค้าใหม่ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                if (product.ImageFile != null)
                    product.ImageUrl = await SaveImage(product.ImageFile);

                product.Attributes = product.Attributes?
                    .Where(a => !string.IsNullOrEmpty(a.AttributeName))
                    .ToList() ?? new List<ProductAttribute>();

                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "เพิ่มสินค้าเรียบร้อยแล้ว";
                return RedirectToAction(nameof(Index));
            }

            await LoadCategoryDropdown(product.CategoryId);
            return View(product);
        }

        // 4. หน้าแก้ไขสินค้า (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Attributes)
                .Include(p => p.Category)
                    .ThenInclude(c => c!.ParentCategory)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            await LoadCategoryDropdown(product.CategoryId);
            return View(product);
        }

        // 5. บันทึกการแก้ไข (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var existingProduct = await _context.Products
                    .Include(p => p.Attributes)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingProduct == null) return NotFound();

                if (product.ImageFile != null)
                {
                    DeleteExistingImage(existingProduct.ImageUrl);
                    existingProduct.ImageUrl = await SaveImage(product.ImageFile);
                }

                existingProduct.Name = product.Name;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Description = product.Description;
                existingProduct.FullContent = product.FullContent;
                existingProduct.GeneralFeatures = product.GeneralFeatures;

                if (existingProduct.Attributes != null)
                    _context.ProductAttributes.RemoveRange(existingProduct.Attributes);

                existingProduct.Attributes = product.Attributes?
                    .Where(a => !string.IsNullOrEmpty(a.AttributeName))
                    .Select(a => new ProductAttribute
                    {
                        AttributeName = a.AttributeName,
                        AttributeValue = a.AttributeValue
                    })
                    .ToList() ?? new List<ProductAttribute>();

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "อัปเดตสินค้าเรียบร้อยแล้ว";
                return RedirectToAction(nameof(Index));
            }

            await LoadCategoryDropdown(product.CategoryId);
            return View(product);
        }

        // 6. หน้าจอยืนยันการลบสินค้า (GET)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // 6.1 ยืนยันการลบทิ้ง (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.Attributes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product != null)
            {
                // ลบรูปภาพจากโฟลเดอร์ uploads
                DeleteExistingImage(product.ImageUrl);

                // ลบ Attributes ที่ผูกอยู่
                if (product.Attributes != null)
                    _context.ProductAttributes.RemoveRange(product.Attributes);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "ลบข้อมูลสินค้าออกจากระบบเรียบร้อยแล้ว";
            }

            return RedirectToAction(nameof(Index));
        }

        // 7. หน้าดูรายละเอียดสินค้า (GET)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            // ดึงข้อมูลสินค้า พร้อมกับ หมวดหมู่ และ คุณสมบัติ (Attributes)
            var product = await _context.Products
                .Include(p => p.Attributes)
                .Include(p => p.Category)
                    .ThenInclude(c => c!.ParentCategory)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // ── helpers ──────────────────────────────────────────────────
        private async Task<string> SaveImage(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string path = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(stream);

            return "/uploads/" + fileName;
        }

        private void DeleteExistingImage(string? imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var path = Path.Combine(_hostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
        }
    }
}