using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMEnergyWeb.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "กรุณากรอกชื่อสินค้า")]
        [Display(Name = "ชื่อสินค้า")]
        public string Name { get; set; } = string.Empty;

        // เชื่อมคอลัมน์ "Category" ใน SQL ให้เปลี่ยนมารองรับระบบ ID ตัวเลข
        [Required(ErrorMessage = "กรุณาเลือกหมวดหมู่สินค้า")]
        [Column("Category")]
        [Display(Name = "หมวดหมู่สินค้า")]
        public int CategoryId { get; set; }

        // ดึงคุณสมบัติของหมวดหมู่มาร่วมใช้งาน
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        [Display(Name = "สรุปสั้นๆ")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "รายละเอียดเชิงลึก")]
        public string? FullContent { get; set; }

        [Display(Name = "รูปภาพสินค้า")]
        public string? ImageUrl { get; set; }

        [NotMapped]
        [Display(Name = "อัปโหลดรูปภาพ")]
        public IFormFile? ImageFile { get; set; }

        public string? GeneralFeatures { get; set; }

        [Display(Name = "ข้อมูลตาราง JSON")]
        public string? TableDataJson { get; set; }

        public virtual ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    }
}