using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PPMEnergyWeb.Models;

namespace PPMEnergyWeb.Helpers
{
    // Helper กลางสำหรับอ่าน/เขียนตะกร้าสินค้าใน Session
    // ใช้ร่วมกันระหว่าง CartController และ _Layout.cshtml (แสดงจำนวนไอเทมบนไอคอนตะกร้า)
    public static class CartHelper
    {
        private const string CartSessionKey = "PPM_Cart";

        public static List<CartItem> GetCart(ISession session)
        {
            var json = session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(json)) return new List<CartItem>();

            try
            {
                return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        public static void SaveCart(ISession session, List<CartItem> cart)
        {
            session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        }

        public static void ClearCart(ISession session)
        {
            session.Remove(CartSessionKey);
        }
    }
}
