using System.ComponentModel.DataAnnotations.Schema;

namespace PPMEnergyWeb.Models
{
    // ตารางกลางเชื่อม Supplier ↔ Category (1 supplier ผูกได้หลายหมวดหมู่, 1 หมวดหมู่มีได้หลาย supplier)
    public class SupplierCategory
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public Supplier Supplier { get; set; } = null!;

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;
    }
}
