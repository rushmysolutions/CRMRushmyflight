namespace RushMyBookings.Crm.Entities;

public class BookComment
{
    public int Id { get; set; }
    public string BookingId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Comment { get; set; }
    public string? CreatedDate { get; set; }
}
