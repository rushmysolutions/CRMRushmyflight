namespace RushMyBookings.Crm.Entities;

public class BookingType
{
    public int TypeId { get; set; }
    public string? TypeName { get; set; }
    public int Status { get; set; } = 1;
    public int Border { get; set; }
}
