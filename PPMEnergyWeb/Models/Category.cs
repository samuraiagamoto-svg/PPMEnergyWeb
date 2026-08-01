using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMEnergyWeb.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "กรุณาระบุชื่อหมวดหมู่")]
        [Display(Name = "ชื่อหมวดหมู่")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุรหัส URL")]
        [Display(Name = "รหัส URL (Slug)")]
        public string CategoryCode { get; set; } = string.Empty;

        [Display(Name = "ลำดับการแสดงผล")]
        public int DisplayOrder { get; set; } = 0;

        [Display(Name = "หมวดหมู่หลัก (เว้นว่างถ้าต้องการสร้างเป็นหมวดหมู่หลัก)")]
        public int? ParentCategoryId { get; set; }

        [ForeignKey("ParentCategoryId")]
        public virtual Category? ParentCategory { get; set; }

        // ─── 🛠️ เพิ่ม 2 บรรทัดนี้สำหรับระบบรูปภาพ ───

        [Display(Name = "รูปภาพหมวดหมู่")]
        public string? ImageUrl { get; set; } // คอลัมน์นี้จะถูกสร้างใน Database

        [NotMapped]
        [Display(Name = "อัปโหลดรูปภาพ")]
        public IFormFile? ImageFile { get; set; } // ตัวนี้จะไม่ถูกสร้างใน Database (ใช้รับไฟล์ชั่วคราว)

        // ───────────────────────────────────────────

        [NotMapped]
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    }
}