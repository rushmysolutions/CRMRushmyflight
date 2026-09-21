namespace RushMyBookings.Crm.Entities;

public class BookAttachment
{
    public int AttachId { get; set; }
    public int BookingId { get; set; }

    public string? AttachFiles { get; set; }
    public int InitialUploadQuality { get; set; }

    public BookFlightDetail? Booking { get; set; }
}
