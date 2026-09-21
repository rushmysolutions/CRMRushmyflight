namespace RushMyBookings.Crm.Entities;

public class PassengerList
{
    public int PassId { get; set; }
    public int BookingId { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Dob { get; set; }
    public string? Gender { get; set; }
    public string? TicketNo { get; set; }

    public BookFlightDetail? Booking { get; set; }
}
