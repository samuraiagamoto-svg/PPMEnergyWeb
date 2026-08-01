using Microsoft.AspNetCore.Identity;

namespace PPMEnergyWeb.Models
{
	// ต้องเป็น : IdentityUser นะครับ ห้ามเป็น : ApplicationUser
	public class ApplicationUser : IdentityUser
	{
		public string? ShippingAddress { get; set; }
        public string? CompanyName { get; set; } // 📍 [NEW] ชื่อบริษัทของลูกค้า เก็บตอนสมัครสมาชิก
        public DateTime CreatedDate { get; set; } = DateTime.Now; // ← เพิ่มบรรทัดนี้
    }
}