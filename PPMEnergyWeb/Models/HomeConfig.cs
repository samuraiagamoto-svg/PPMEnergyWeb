namespace PPMEnergyWeb.Models
{
    public class HomeConfig
    {
        public int Id { get; set; }
        public string? HeroTitle { get; set; }      // หัวข้อใหญ่หน้าแรก
        public string? HeroDescription { get; set; } // รายละเอียด
        public string? BannerImageUrl { get; set; }   // พาธรูปแบนเนอร์
        public string? ContactPhone { get; set; }    // เบอร์โทรติดต่อหน้าแรก
    }
}