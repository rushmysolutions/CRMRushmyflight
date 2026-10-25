using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Models
{
    public class ManageIpViewModel
    {
        public int UserId { get; set; }
        public string? Username { get; set; }

        [Required(ErrorMessage = "Allowed IP Address is required")]
        [MaxLength(250, ErrorMessage = "Max 250 characters allowed")]
        public string? AllowedIps { get; set; }

        // display only
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? UserType { get; set; }
        public bool IsActive { get; set; }
        public bool IsAllUsers { get; set; }

    }
}
