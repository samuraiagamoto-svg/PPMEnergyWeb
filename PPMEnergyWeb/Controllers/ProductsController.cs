using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;

namespace PPMEnergyWeb.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        public async Task<IActionResult> Index(string? category, string? sub)
        {
            // โหลด categories ทั้งหมดในครั้งเดียว
            // (ไม่ใช้ Include SubCategories เพราะเป็น [NotMapped] แล้ว map เอง)
            var allCategories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var parentCategories = allCategories
                .Where(c => c.ParentCategoryId == null)
                .ToList();

            var allSubCategories = allCategories
                .Where(c => c.ParentCategoryId != null)
                .ToList();

            // map SubCategories manually เพราะ [NotMapped]
            foreach (var p in parentCategories)
                p.SubCategories = allSubCategories
                    .Where(s => s.ParentCategoryId == p.Id)
                    .ToList();

            // ── Step 1: ยังไม่เลือก category → แสดง main category cards ──
            if (string.IsNullOrEmpty(category))
            {
                ViewData["CurrentCategoryCode"] = null;
                ViewData["CurrentSubCode"] = null;
                ViewData["ParentCategories"] = parentCategories;
                ViewData["AllSubCategories"] = allSubCategories;
                return View(Enumerable.Empty<Product>());
            }

            var mainCat = parentCategories
                .FirstOrDefault(c => c.CategoryCode.ToLower().Trim() == category.ToLower().Trim());

            if (mainCat == null) return NotFound();

            var subsOfMain = allSubCategories
                .Where(s => s.ParentCategoryId == mainCat.Id)
                .ToList();

            // ── Step 2: Main มี Sub และยังไม่เลือก sub → แสดง sub cards ──
            if (subsOfMain.Any() && string.IsNullOrEmpty(sub))
            {
                ViewData["CurrentCategoryCode"] = category;
                ViewData["CurrentSubCode"] = null;
                ViewData["ParentCategories"] = parentCategories;
                ViewData["AllSubCategories"] = allSubCategories;
                return View(Enumerable.Empty<Product>());
            }

            // ── Step 3a: Main ไม่มี Sub → แสดงสินค้าใน Main โดยตรง ──────
            // เช่น "น้ำมันเตา" ไม่มี sub-category สินค้าผูกกับ main โดยตรง
            if (!subsOfMain.Any() && string.IsNullOrEmpty(sub))
            {
                var productsInMain = await _context.Products
                    .Include(p => p.Attributes)
                    .Include(p => p.Category)
                        .ThenInclude(c => c!.ParentCategory)
                    .Where(p => p.CategoryId == mainCat.Id)
                    .ToListAsync();

                ViewData["CurrentCategoryCode"] = category;
                ViewData["CurrentSubCode"] = category; // set ให้ View เข้า step 3
                ViewData["ParentCategories"] = parentCategories;
                ViewData["AllSubCategories"] = allSubCategories;
                ViewData["ActiveSubName"] = mainCat.DisplayName;
                return View(productsInMain);
            }

            // ── Step 3b: เลือก Sub → แสดงสินค้าใน Sub ───────────────────
            var subCat = allSubCategories
                .FirstOrDefault(c => c.CategoryCode.ToLower().Trim() == sub!.ToLower().Trim());

            if (subCat == null) return NotFound();

            var products = await _context.Products
                .Include(p => p.Attributes)
                .Include(p => p.Category)
                    .ThenInclude(c => c!.ParentCategory)
                .Where(p => p.CategoryId == subCat.Id)
                .ToListAsync();

            ViewData["CurrentCategoryCode"] = category;
            ViewData["CurrentSubCode"] = sub;
            ViewData["ParentCategories"] = parentCategories;
            ViewData["AllSubCategories"] = allSubCategories;

            return View(products);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Attributes)
                .Include(p => p.Category)
                    .ThenInclude(c => c!.ParentCategory)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            var related = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4)
                .ToListAsync();
            ViewBag.RelatedProducts = related;

            return View(product);
        }

        private bool ProductExists(int id) =>
            _context.Products.Any(e => e.Id == id);
    }
}