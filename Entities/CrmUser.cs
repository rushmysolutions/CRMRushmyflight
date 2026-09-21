namespace RushMyBookings.Crm.Entities;

public class CrmUser
{
    public int UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string Status { get; set; } = "1";
    public string? UserType { get; set; }
    public string? Username { get; set; }
    public int? LastLogin { get; set; }
}
