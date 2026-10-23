using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Models
{
    public class ChangePasswordSaleViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "New Password is required")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Password must be 6–50 characters")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Please confirm the password")]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}
