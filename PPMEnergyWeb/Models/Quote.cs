using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PPMEnergyWeb.Models
{
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; } // เก็บเผื่อกรณีเป็นสมาชิกที่ล็อกอินเข้ามา

        // --- ข้อมูลผู้ติดต่อ (Contact Info) ---
        [Required(ErrorMessage = "กรุณาระบุชื่อบริษัท")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุชื่อผู้ติดต่อ")]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุอีเมล")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุเบอร์โทรศัพท์")]
        public string Phone { get; set; } = string.Empty;

        // --- ชื่อผู้ขาย (พนักงาน/แอดมินที่ดูแลใบเสนอราคานี้) กรอกโดยแอดมินตอนทำราคา ---
        public string? SalesPersonName { get; set; }

        // --- รายละเอียดการจัดส่ง (Delivery Info) ---
        [Required(ErrorMessage = "กรุณาระบุสถานที่จัดส่ง")]
        public string DeliveryLocation { get; set; } = string.Empty;

        public string? Note { get; set; }

        // --- ข้อมูลระบบ (System Info) ---
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public QuoteStatus Status { get; set; } = QuoteStatus.New;

        // --- ความสัมพันธ์ (Relationships) ---
        // 1 ใบคำขอ มีรายละเอียดสินค้าได้ (เผื่ออนาคตพัฒนาระบบตะกร้าหลายชิ้น)
        public List<QuoteDetail> QuoteDetails { get; set; } = new List<QuoteDetail>();
    }

}