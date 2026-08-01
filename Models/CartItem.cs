namespace PPMEnergyWeb.Models
{
    // ตัวแทนสินค้า 1 รายการในตะกร้า เก็บไว้ใน Session (ยังไม่บันทึกลง DB จนกว่าจะกด "ส่งขอใบเสนอราคา")
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public double Quantity { get; set; }
    }
}
