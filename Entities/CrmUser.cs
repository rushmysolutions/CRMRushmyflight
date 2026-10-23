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
    public string? Phone { get; set; }
    public string? CopyPass { get; set; }
    public string? Dob { get; set; }
    public short? Sex { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public int? CreatedDate { get; set; }        // unix timestamp
    public string DialerId { get; set; } = "0";
    public string? AllowedIps { get; set; }
    public string? TollFreeNo { get; set; }
    public string? DirectLine { get; set; }
    public string? Extn { get; set; }
}
