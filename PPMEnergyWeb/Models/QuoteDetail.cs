using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PPMEnergyWeb.Models
{
    public class QuoteDetail
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key เชื่อมไปยังตาราง Quote หลัก
        public int QuoteId { get; set; }
        [ForeignKey("QuoteId")]
        public Quote Quote { get; set; } = null!;

        // ข้อมูลสินค้าที่ลูกค้าเลือก
        public int ProductId { get; set; }

        // แนะนำให้เก็บชื่อสินค้า ณ วันที่ขอ เผื่ออนาคตชื่อสินค้าในระบบถูกเปลี่ยน
        public string ProductName { get; set; } = string.Empty;

        // จำนวนที่ขอ (ตรงกับ name="qty" ในฟอร์ม)
        public double Quantity { get; set; }
        public decimal Price { get; set; }
    }
}