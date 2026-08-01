using System.ComponentModel.DataAnnotations;

namespace PPMEnergyWeb.Models.AccountViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "กรุณากรอกอีเมล")]
        [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
        [Display(Name = "อีเมล")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกรหัสผ่านใหม่")]
        [StringLength(100, ErrorMessage = "รหัสผ่านต้องมีอย่างน้อย {2} ตัวอักษร", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "รหัสผ่านใหม่")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "ยืนยันรหัสผ่านใหม่")]
        [Compare("Password", ErrorMessage = "รหัสผ่านไม่ตรงกัน")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // ส่งมาจาก query string ตอนคลิกลิงก์ในอีเมล (ซ่อนในฟอร์ม)
        public string Code { get; set; } = string.Empty;
    }
}
