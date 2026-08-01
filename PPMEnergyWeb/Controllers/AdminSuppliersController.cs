using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;

namespace PPMEnergyWeb.Controllers
{
    public class AdminSuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminSuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // โหลดรายการหมวดหมู่ทั้งหมด (หลัก + ย่อย) เรียงสวยงาม สำหรับ checkbox ในหน้า Create/Edit
        private async Task<List<Category>> LoadAllCategoriesForCheckbox()
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategoryId == null ? 0 : 1)
                .ThenBy(c => c.ParentCategoryId)
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        // GET: /AdminSuppliers
        public async Task<IActionResult> Index(string? search, int? categoryId, bool? activeOnly)
        {
            var query = _context.Suppliers
                .Include(s => s.SupplierCategories).ThenInclude(sc => sc.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(s =>
                    s.CompanyName.ToLower().Contains(search) ||
                    (s.ContactPersonName != null && s.ContactPersonName.ToLower().Contains(search)) ||
                    (s.Email != null && s.Email.ToLower().Contains(search)) ||
                    (s.Phone != null && s.Phone.Contains(search)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(s => s.SupplierCategories.Any(sc => sc.CategoryId == categoryId.Value));
            }

            if (activeOnly == true)
            {
                query = query.Where(s => s.IsActive);
            }

            var suppliers = await query.OrderBy(s => s.CompanyName).ToListAsync();

            ViewBag.Categories = await LoadAllCategoriesForCheckbox();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.ActiveOnly = activeOnly;
            ViewBag.TotalCount = await _context.Suppliers.CountAsync();
            ViewBag.ActiveCount = await _context.Suppliers.CountAsync(s => s.IsActive);

            return View(suppliers);
        }

        // GET: /AdminSuppliers/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.AllCategories = await LoadAllCategoriesForCheckbox();
            return View(new Supplier { IsActive = true });
        }

        // POST: /AdminSuppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier, int[] categoryIds)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllCategories = await LoadAllCategoriesForCheckbox();
                return View(supplier);
            }

            supplier.CreatedDate = DateTime.Now;

            if (categoryIds != null)
            {
                foreach (var catId in categoryIds.Distinct())
                {
                    supplier.SupplierCategories.Add(new SupplierCategory { CategoryId = catId });
                }
            }

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"เพิ่มซัพพลายเออร์ \"{supplier.CompanyName}\" สำเร็จ";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminSuppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers
                .Include(s => s.SupplierCategories)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();

            ViewBag.AllCategories = await LoadAllCategoriesForCheckbox();
            ViewBag.SelectedCategoryIds = supplier.SupplierCategories.Select(sc => sc.CategoryId).ToList();

            return View(supplier);
        }

        // POST: /AdminSuppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier, int[] categoryIds)
        {
            if (id != supplier.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.AllCategories = await LoadAllCategoriesForCheckbox();
                ViewBag.SelectedCategoryIds = categoryIds?.ToList() ?? new List<int>();
                return View(supplier);
            }

            var existing = await _context.Suppliers
                .Include(s => s.SupplierCategories)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();

            existing.CompanyName = supplier.CompanyName;
            existing.ContactPersonName = supplier.ContactPersonName;
            existing.Email = supplier.Email;
            existing.Phone = supplier.Phone;
            existing.LineId = supplier.LineId;
            existing.Address = supplier.Address;
            existing.TaxId = supplier.TaxId;
            existing.CreditTerm = supplier.CreditTerm;
            existing.IsActive = supplier.IsActive;
            existing.Note = supplier.Note;

            // อัปเดตหมวดหมู่ที่ผูกไว้ทั้งหมด (ลบของเดิมแล้วเพิ่มใหม่ตามที่เลือกล่าสุด)
            _context.SupplierCategories.RemoveRange(existing.SupplierCategories);
            if (categoryIds != null)
            {
                foreach (var catId in categoryIds.Distinct())
                {
                    existing.SupplierCategories.Add(new SupplierCategory { CategoryId = catId });
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"บันทึกข้อมูลซัพพลายเออร์ \"{existing.CompanyName}\" สำเร็จ";
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminSuppliers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            _context.Suppliers.Remove(supplier); // SupplierCategories ลบตามด้วย Cascade
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"ลบซัพพลายเออร์ \"{supplier.CompanyName}\" แล้ว";
            return RedirectToAction(nameof(Index));
        }
    }
}
