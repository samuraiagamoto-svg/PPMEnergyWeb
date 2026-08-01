
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PPMEnergyWeb.Models;
using PPMEnergyWeb.Models.AccountViewModels;
using System.Threading.Tasks;

namespace PPMEnergyWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager; // เปลี่ยน
        private readonly UserManager<ApplicationUser> _userManager;      // เปลี่ยน
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;                      // เพิ่มสำหรับ Forgot Password

        public AccountController(
            UserManager<ApplicationUser> userManager,              // เปลี่ยน
            SignInManager<ApplicationUser> signInManager,          // เปลี่ยน
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)                               // เพิ่มสำหรับ Forgot Password
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }
        // ...

        // --- หน้าเข้าสู่ระบบ (Login) ---
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // ส่ง returnUrl ไปที่ View เพื่อให้ฟอร์มเก็บไว้
            ViewData["ReturnUrl"] = returnUrl;

            // ถ้า Login ค้างไว้อยู่แล้ว ให้ดีดไปหน้าที่ควรจะอยู่ทันที
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectByUserRole();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    // 1. ถ้ามี ReturnUrl ให้กลับไปที่หน้านั้นทันที (เช่น หน้าขอใบเสนอราคา)
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // 2. ถ้าไม่มี ReturnUrl ให้ตรวจสอบสิทธิ์เพื่อเลือกหน้า Dashboard
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        var isAdmin = await _userManager.IsInRoleAsync(user, "9");
                        if (isAdmin)
                        {
                            return RedirectToAction("Index", "Admin");
                        }

                        // ✅ เพิ่มตรงนี้
                        var isOwner = await _userManager.IsInRoleAsync(user, "Owner");
                        if (isOwner)
                        {
                            return RedirectToAction("Index", "OwnerReport");
                        }
                    }

                }


                ModelState.AddModelError(string.Empty, "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
            }
            return View(model);
        }

        // --- หน้าสมัครสมาชิก (Register) ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, CompanyName = model.CompanyName };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // สร้าง Role "1" (Customer) และ "9" (Admin) หากยังไม่มีในระบบ
                    await EnsureRolesAsync();

                    // สมัครใหม่ให้เป็น Role "1" (Customer) เสมอ
                    await _userManager.AddToRoleAsync(user, "1");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        // --- ออกจากระบบ (Logout) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // --- หน้าแสดงเมื่อไม่มีสิทธิ์เข้าถึง ---
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ════════════════════════════════════════════════════════
        // ลืมรหัสผ่าน (Forgot Password)
        // ════════════════════════════════════════════════════════

        // STEP 1: หน้ากรอกอีเมลเพื่อขอลิงก์รีเซ็ตรหัสผ่าน
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            // ⚠️ ไม่บอกว่าอีเมลนี้มีในระบบหรือไม่ (ป้องกัน user enumeration)
            // หมายเหตุ: โปรเจคนี้ไม่มี flow ยืนยันอีเมลตอนสมัคร (Register ไม่ได้ set EmailConfirmed = true)
            // จึงไม่เช็ค IsEmailConfirmedAsync ที่นี่ ไม่งั้นจะไม่มีใครรีเซ็ตรหัสผ่านได้เลย
            if (user == null)
            {
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // สร้าง token รีเซ็ตรหัสผ่าน
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Action(
                action: nameof(ResetPassword),
                controller: "Account",
                values: new { code, email = user.Email },
                protocol: Request.Scheme);

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto;'>
                    <h2 style='color:#0a8f08;'>รีเซ็ตรหัสผ่าน PPM Energy</h2>
                    <p>เราได้รับคำขอรีเซ็ตรหัสผ่านสำหรับบัญชีของคุณ</p>
                    <p>คลิกปุ่มด้านล่างเพื่อตั้งรหัสผ่านใหม่ (ลิงก์นี้จะหมดอายุภายใน 1 ชั่วโมง):</p>
                    <p style='text-align:center; margin: 24px 0;'>
                        <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'
                           style='background:#0a8f08;color:#fff;padding:12px 28px;border-radius:8px;
                                  text-decoration:none;font-weight:bold;display:inline-block;'>
                           ตั้งรหัสผ่านใหม่
                        </a>
                    </p>
                    <p style='color:#888;font-size:13px;'>
                        หากคุณไม่ได้เป็นผู้ขอเปลี่ยนรหัสผ่าน กรุณาเพิกเฉยต่ออีเมลฉบับนี้
                    </p>
                </div>";

            await _emailSender.SendEmailAsync(user.Email!, "รีเซ็ตรหัสผ่าน - PPM Energy", emailBody);

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // STEP 2: หน้าตั้งรหัสผ่านใหม่ (เข้ามาจากลิงก์ในอีเมล)
        [HttpGet]
        public IActionResult ResetPassword(string? code, string? email)
        {
            if (code == null || email == null)
            {
                // ไม่มี token หรือ email แนบมา แสดงว่าเข้าหน้านี้ไม่ผ่าน flow ที่ถูกต้อง
                return BadRequest("ลิงก์รีเซ็ตรหัสผ่านไม่ถูกต้อง");
            }

            var model = new ResetPasswordViewModel
            {
                Code = code,
                Email = email
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // ไม่บอกเหตุผลตรงๆ ว่าหา user ไม่เจอ กันการเดาอีเมลที่มีในระบบ
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "ลิงก์รีเซ็ตรหัสผ่านไม่ถูกต้องหรือหมดอายุ");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedCode, model.Password);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            // token หมดอายุ / ใช้ไปแล้ว / รหัสผ่านไม่ผ่านเงื่อนไข ฯลฯ
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        #region Helper Methods

        // ฟังก์ชันช่วยตรวจสอบและสร้าง Role อัตโนมัติ
        private async Task EnsureRolesAsync()
        {
            string[] roleNames = { "1", "9" };
            foreach (var roleName in roleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        // ฟังก์ชันช่วย Redirect ตามสิทธิ์ (ใช้ตอนเข้าหน้า Login ขณะที่ยังไม่ได้ Logout)
        private IActionResult RedirectByUserRole()
        {
            if (User.IsInRole("9"))
            {
                return RedirectToAction("Index", "Admin");
            }
            return RedirectToAction("Index", "Home");
        }

        #endregion
    }
}
