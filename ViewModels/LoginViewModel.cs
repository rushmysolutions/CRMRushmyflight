using System.ComponentModel.DataAnnotations;
using RushMyBookings.Crm.Entities.Attendance;

namespace RushMyBookings.Crm.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Please select CRM or Internal.")]
    [Display(Name = "Login as")]
    public string Portal { get; set; } = LoginPortals.Crm;

    [Required(ErrorMessage = "Username or email is required.")]
    [Display(Name = "Username or email")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
