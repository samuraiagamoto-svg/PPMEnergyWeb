using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Helpers;
using PPMEnergyWeb.Hubs;
using PPMEnergyWeb.Models;
using PPMEnergyWeb.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Controllers
{
    // ตะกร้าสินค้า: ให้ลูกค้าเลือกสินค้าได้หลายรายการ/หลายประเภท ก่อนส่งเป็นใบขอเสนอราคาใบเดียว
    // ต้องล็อกอินก่อนถึงจะเพิ่มลงตะกร้า/เช็คเอาท์ได้ (เหมือน flow ขอใบเสนอราคาเดิม)
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<QuoteHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public CartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<QuoteHub> hubContext,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _emailService = emailService;
            _configuration = configuration;
        }

        // POST: /Cart/Add  (ปุ่ม "เพิ่มลงตะกร้า" จากหน้ารายละเอียดสินค้า)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, double qty, string? returnUrl)
        {
            if (qty <= 0) qty = 1;

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "ไม่พบสินค้านี้ในระบบ";
                return Redirect(returnUrl ?? Url.Action("Index", "Products")!);
            }

            var cart = CartHelper.GetCart(HttpContext.Session);
            var existing = cart.FirstOrDefault(c => c.ProductId == productId);
            if (existing != null)
            {
                existing.Quantity += qty;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ImageUrl = product.ImageUrl,
                    Quantity = qty
                });
            }
            CartHelper.SaveCart(HttpContext.Session, cart);

            TempData["SuccessMessage"] = $"เพิ่ม \"{product.Name}\" ลงตะกร้าแล้ว ({cart.Count} รายการในตะกร้า)";
            return Redirect(returnUrl ?? Url.Action("Index", "Products")!);
        }

        // POST: /Cart/UpdateQty
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQty(int productId, double qty)
        {
            var cart = CartHelper.GetCart(HttpContext.Session);
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
            if (item != null)
            {
                item.Quantity = qty <= 0 ? 1 : qty;
                CartHelper.SaveCart(HttpContext.Session, cart);
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId)
        {
            var cart = CartHelper.GetCart(HttpContext.Session);
            cart.RemoveAll(c => c.ProductId == productId);
            CartHelper.SaveCart(HttpContext.Session, cart);
            return RedirectToAction(nameof(Index));
        }

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            var cart = CartHelper.GetCart(HttpContext.Session);
            ViewBag.Cart = cart;

            // เตรียมข้อมูลเริ่มต้นของฟอร์มขอใบเสนอราคาจากโปรไฟล์ผู้ใช้ที่ล็อกอินอยู่
            var user = await _userManager.GetUserAsync(User);
            var model = new Quote
            {
                CompanyName = user?.CompanyName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Phone = user?.PhoneNumber ?? string.Empty,
                DeliveryLocation = user?.ShippingAddress ?? string.Empty
            };

            return View(model);
        }

        // POST: /Cart/Checkout — สร้างใบเสนอราคา 1 ใบ ที่มีสินค้าได้หลายรายการจากตะกร้า
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Quote model)
        {
            ModelState.Remove("QuoteDetails");

            var cart = CartHelper.GetCart(HttpContext.Session);
            if (cart.Count == 0)
            {
                TempData["ErrorMessage"] = "ตะกร้าของคุณว่างเปล่า กรุณาเลือกสินค้าก่อนส่งขอใบเสนอราคา";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cart = cart;
                return View("Index", model);
            }

            model.CreatedDate = DateTime.Now;
            model.Status = QuoteStatus.New;
            model.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            model.QuoteDetails = cart.Select(c => new QuoteDetail
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                Quantity = c.Quantity
            }).ToList();

            _context.Quotes.Add(model);
            await _context.SaveChangesAsync();

            // เคลียร์ตะกร้าหลังส่งสำเร็จ
            CartHelper.ClearCart(HttpContext.Session);

            string productSummary = string.Join(", ", model.QuoteDetails.Select(d => $"{d.ProductName} ({d.Quantity:N0})"));

            // แจ้งเตือนผ่าน SignalR เหมือน flow เดิม
            await _hubContext.Clients.All.SendAsync("ReceiveNewQuote", model.CompanyName, productSummary);

            // ส่งอีเมลแจ้งแอดมิน พร้อมรายการสินค้าทั้งหมดในตะกร้า
            try
            {
                var adminEmail = _configuration["EmailSettings:AdminEmail"];
                string emailSubject = $"[ใบเสนอราคาใหม่] จาก {model.CompanyName} - ผู้ติดต่อ: {model.ContactName}";

                var itemRows = string.Join("", model.QuoteDetails.Select(d => $@"
                    <tr>
                        <td style='padding:6px 0;border-bottom:1px solid #edf2f7'>{d.ProductName}</td>
                        <td style='padding:6px 0;border-bottom:1px solid #edf2f7;text-align:right'>{d.Quantity:N0}</td>
                    </tr>"));

                string emailBody = $@"
                    <div style='font-family: sans-serif; padding: 20px; max-width: 600px; border: 1px solid #e2e8f0; border-radius: 16px; background-color: #F8F8FF;'>
                        <h2 style='color: #009933; margin-bottom: 20px; font-size: 22px;'>🔔 มีคำขอใบเสนอราคาใหม่เข้าระบบ (หลายรายการ)</h2>
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
                                <td style='padding: 10px 0; font-weight: bold; color: #4a5568; vertical-align:top; border-bottom: 1px solid #edf2f7;'>สินค้าที่สนใจ:</td>
                                <td style='padding: 10px 0; border-bottom: 1px solid #edf2f7;'>
                                    <table style='width:100%;border-collapse:collapse'>
                                        <thead>
                                            <tr>
                                                <th style='text-align:left;font-size:12px;color:#a0aec0;padding-bottom:4px'>สินค้า</th>
                                                <th style='text-align:right;font-size:12px;color:#a0aec0;padding-bottom:4px'>จำนวน</th>
                                            </tr>
                                        </thead>
                                        <tbody>{itemRows}</tbody>
                                    </table>
                                </td>
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

            TempData["SuccessMessage"] = "ส่งข้อมูลขอใบเสนอราคาสำเร็จ! ทีมงานจะรีบติดต่อกลับโดยเร็วที่สุด";
            return RedirectToAction("Index", "Products");
        }
    }
}
