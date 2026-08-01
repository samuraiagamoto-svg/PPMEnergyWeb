using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Data;
using PPMEnergyWeb.Hubs;
using PPMEnergyWeb.Middleware;
using PPMEnergyWeb.Models;
using PPMEnergyWeb.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using PPMEnergyWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. ลงทะเบียน Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ลงทะเบียน SignalR Service
builder.Services.AddSignalR();

// 📍 [NEW] ลงทะเบียน Email Service สำหรับแจ้งเตือนใบเสนอราคาเข้าเมลบริษัท
builder.Services.AddTransient<IEmailService, EmailService>();

// 3. ลงทะเบียน ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 4. ตั้งค่า Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
});

// 5. Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 📍 [NEW] เพิ่ม Session สำหรับเก็บตะกร้าสินค้า (Cart) ของลูกค้าแต่ละคน
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ════════════════════════════════════════════════════════════
// เพิ่ม/แก้โค้ดส่วนนี้ใน Program.cs ของคุณ (ก่อน builder.Build())
// ════════════════════════════════════════════════════════════

// 1. ลงทะเบียน EmailService เดิมของคุณ (ใช้ MailKit อยู่แล้ว)
//    *** ถ้ามีบรรทัดนี้อยู่แล้วใน Program.cs ไม่ต้องเพิ่มซ้ำ ***
builder.Services.AddTransient<IEmailService, EmailService>();

// 2. เพิ่มตัวเชื่อม (Adapter) ให้ ASP.NET Identity เรียกใช้ EmailService ตัวเดิมได้
//    ทำให้ Forgot Password กับ Quote ใช้โค้ดส่งอีเมลชุดเดียวกัน (MailKit)
builder.Services.AddTransient<IEmailSender, IdentityEmailAdapter>();

// 3. (แนะนำ) ปรับอายุ token รีเซ็ตรหัสผ่านให้สั้นลงเหลือ 1 ชั่วโมง (ค่า default ของ Identity คือ 1 วัน)
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

// ════════════════════════════════════════════════════════════
// อย่าลืม using ด้านบนของไฟล์ Program.cs:
// using Microsoft.AspNetCore.Identity.UI.Services;
// using PPMEnergyWeb.Services;
// ════════════════════════════════════════════════════════════


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 📍 [NEW] เปิดใช้งาน Session (ต้องมาก่อน UseAuthorization และก่อนใช้ Session ในหน้าอื่น)
app.UseSession();

// --- [NEW] ลงทะเบียน Custom Middleware สำหรับนับจำนวนผู้เข้าชม ---
app.UseMiddleware<VisitorTrackerMiddleware>();

// 6. การจัดการสิทธิ์ (Authentication/Authorization)
app.UseAuthentication();
app.UseAuthorization();

// --- [NEW] Map เส้นทางของ SignalR Hub ---
app.MapHub<QuoteHub>("/quoteHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Seed Roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "1", "9", "Owner" };
    foreach (var role in roles)
    {
        if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
    }
}

app.Run();