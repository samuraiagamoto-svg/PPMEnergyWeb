using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMEnergyWeb.Models
{
    // ✅ เพิ่มบรรทัดนี้เพื่อระบุชื่อตารางให้ตรงกับ dbo.ProductAttributes ใน Database
    [Table("ProductAttributes")]
    public class ProductAttribute
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required]
        [Display(Name = "หัวข้อสเปก")]
        public string AttributeName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "ค่าสเปก")]
        public string AttributeValue { get; set; } = string.Empty;

        // เชื่อมกลับไปที่ Product หลัก
        [ForeignKey("ProductId")] // ระบุ ForeignKey ให้ชัดเจน
        public virtual Product? Product { get; set; }
    }
}