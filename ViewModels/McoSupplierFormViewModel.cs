using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.ViewModels
{
    public class McoSupplierFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Company Name is required"), StringLength(100)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Ticket Charge Name is required"), StringLength(100)]
        public string TicketCharge { get; set; } = "";

        [Required(ErrorMessage = "TFN is required"), StringLength(20)]
        public string TollFree { get; set; } = "";

        [Required(ErrorMessage = "Email Address is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email"), StringLength(50)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "From Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email"), StringLength(50)]
        public string FromEmail { get; set; } = "";

        // Blank = keep the existing password
        [StringLength(50)]
        public string? NewSmtpPass { get; set; }

        public string? SmtpPass { get; set; }
        public bool HasSmtpPass { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
