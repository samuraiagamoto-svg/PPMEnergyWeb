using System.ComponentModel.DataAnnotations;

namespace PPMEnergyWeb.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "กรุณาระบุชื่อบริษัท")]
        public string CompanyName { get; set; } = string.Empty;

        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
