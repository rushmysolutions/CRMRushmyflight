namespace RushMyBookings.Crm.Entities;

public class BookFlightDetail
{
    public int BookId { get; set; }
    public string? ReferenceNo { get; set; }
    public string? BookingType { get; set; }
    public string? CustomerName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Airline { get; set; }
    public string? Pnr { get; set; }
    public string? TicketStatus { get; set; }
    public double TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? CreatedDate { get; set; }
    public string? TravelDate { get; set; }
    public string? AssignedUser { get; set; }
    public string? FlightInfo { get; set; }
    public string? Supplier { get; set; }
    public string? TicketCost { get; set; }
    public double McoAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string QualityStatus { get; set; } = "0";
    public int IsHide { get; set; }
    public string? FlightPnr { get; set; }
    public string? ReturnDate { get; set; }
    public int NoOfPeople { get; set; } = 1;
    public string? CardHolderName { get; set; }
    public string? CardNumber { get; set; }
    public string? LastFourCardDigits { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Cvv { get; set; }
    public string? InboundPhone { get; set; }
    public string? Address { get; set; }
    public string? Zip { get; set; }
    public string? ClassType { get; set; }
    public string? McoSupplier { get; set; }
    public string? McoStatus { get; set; }
    public string? PartialRefund { get; set; }
    public double NetMco { get; set; }
    public string SplitCharges { get; set; } = "no";
    public string? PaymentUrl { get; set; }
    public string PaymentMode { get; set; } = "manual";
    public string? CustomEmailSubject { get; set; }
    public int BookingLang { get; set; }
    public string? CreatedBy { get; set; }
    public string? TravelPhoneNumber { get; set; }
    public int CustomerAckStatus { get; set; }
    public string? CustomerAckIp { get; set; }
    public string? CustomerAckLocation { get; set; }
    public string? CustomerAckDate { get; set; }
    public int? FollowUpBy { get; set; }
    public string? FollowUpDate { get; set; }

    public ICollection<PassengerList> Passengers { get; set; } = [];
    public ICollection<BookComment> Comments { get; set; } = [];
    public ICollection<BookAttachment> Attachments { get; set; } = [];
}
