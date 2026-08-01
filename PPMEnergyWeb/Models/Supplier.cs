using System.ComponentModel.DataAnnotations;

namespace PPMEnergyWeb.Models
{
    // บริษัทซัพพลายเออร์ที่ PPM ติดต่อ/รับสินค้ามาจำหน่าย
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "กรุณาระบุชื่อบริษัท")]
        [Display(Name = "ชื่อบริษัท")]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "ชื่อผู้ติดต่อ")]
        public string? ContactPersonName { get; set; }

        [Display(Name = "อีเมล")]
        public string? Email { get; set; }

        [Display(Name = "เบอร์โทรศัพท์")]
        public string? Phone { get; set; }

        [Display(Name = "LINE ID")]
        public string? LineId { get; set; }

        [Display(Name = "ที่อยู่บริษัท")]
        public string? Address { get; set; }

        [Display(Name = "เลขประจำตัวผู้เสียภาษี")]
        public string? TaxId { get; set; }

        [Display(Name = "เครดิตเทอม")]
        public string? CreditTerm { get; set; } // เช่น "เงินสด", "เครดิต 30 วัน"

        [Display(Name = "สถานะใช้งาน")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "หมายเหตุ")]
        public string? Note { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // ความสัมพันธ์แบบ many-to-many กับ Category ผ่านตารางกลาง SupplierCategory
        public ICollection<SupplierCategory> SupplierCategories { get; set; } = new List<SupplierCategory>();
    }
}
