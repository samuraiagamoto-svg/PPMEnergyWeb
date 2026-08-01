using System.ComponentModel.DataAnnotations;

namespace PPMEnergyWeb.Models
{
    public enum QuoteStatus
    {
        [Display(Name = "มาใหม่")]
        New,

        [Display(Name = "กำลังดำเนินการ")]
        Processing,

        [Display(Name = "ส่งใบเสนอราคาแล้ว")]
        QuotationSent,

        [Display(Name = "ปิดการขายได้")]
        Won,

        [Display(Name = "ยกเลิก")]
        Lost
    }
}
