namespace RushMyBookings.Crm.Models;

public sealed class BookingSummary
{
    public int BookId { get; init; }
    public string? ReferenceNo { get; init; }
    public string? BookingType { get; init; }
    public string? TypeName { get; init; }
    public string? CustomerName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Airline { get; init; }
    public string? Pnr { get; init; }
    public string? TicketStatus { get; init; }
    public double TotalAmount { get; init; }
    public string? Currency { get; init; }
    public string? CreatedDate { get; init; }
    public string? TravelDate { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public int PassengerCount { get; init; }
    public int CommentCount { get; init; }
    public int AttachmentCount { get; init; }
    public double McoAmount { get; init; }
    public double NetMco { get; init; }
    public int CustomerAckStatus { get; init; }
    public string? McoStatus { get; init; }
    public string? QualityStatus { get; init; }
    public int? FollowUpBy { get; init; }
    public string? FollowUpDate { get; init; }
    public string? CreatedById { get; init; }
    public string? CreatedByName { get; init; }
}

public sealed class BookingDetail
{
    public int BookId { get; init; }
    public string? ReferenceNo { get; init; }
    public string? BookingType { get; init; }
    public string? TypeName { get; init; }
    public string? CustomerName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Airline { get; init; }
    public string? Pnr { get; init; }
    public string? TicketStatus { get; init; }
    public double TotalAmount { get; init; }
    public string? Currency { get; init; }
    public string? CreatedDate { get; init; }
    public string? TravelDate { get; init; }
    public string? ReturnDate { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public string? CreatedById { get; init; }
    public string? CreatedByName { get; init; }
    public int PassengerCount { get; init; }
    public int CommentCount { get; init; }
    public int AttachmentCount { get; init; }
    public string? FlightInfo { get; init; }
    public string? FlightPnr { get; init; }
    public string? Supplier { get; init; }
    public string? TicketCost { get; init; }
    public double McoAmount { get; init; }
    public double NetMco { get; init; }
    public string? PaymentMethod { get; init; }
    public string? PaymentMode { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Country { get; init; }
    public string? QualityStatus { get; init; }
    public string? McoStatus { get; init; }
    public string? McoSupplier { get; init; }
    public string? PartialRefund { get; init; }
    public string? SplitCharges { get; init; }
    public string? PaymentUrl { get; init; }
    public string? ClassType { get; init; }
    public int BookingLang { get; init; }
    public string? CustomEmailSubject { get; init; }
    public string? Tfn { get; init; }
    public string? CardHolderName { get; init; }
    public string? CardNumberMasked { get; init; }
    public string? ExpiryDate { get; init; }
    public string? InboundPhone { get; init; }
    public string? BillingAddress { get; init; }
    public string? ZipCode { get; init; }
    public int CustomerAckStatus { get; init; }
    public string? CustomerAckIp { get; init; }
    public string? CustomerAckLocation { get; init; }
    public string? CustomerAckDate { get; init; }
    public int? FollowUpBy { get; init; }
    public string? FollowUpByName { get; init; }
    public string? FollowUpDate { get; init; }
    public List<PassengerInfo> Passengers { get; set; } = [];
    public List<BookingComment> Comments { get; set; } = [];
    public List<BookingAttachmentInfo> Attachments { get; set; } = [];
}

public sealed class BookingAttachmentInfo
{
    public required string FileName { get; init; }
    public string? DisplayName { get; init; }
    public bool IsImage { get; init; }
}

public sealed class PassengerInfo
{
    public int PassId { get; init; }
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? Dob { get; init; }
    public string? Gender { get; init; }
    public string? TicketNo { get; init; }
}

public sealed class BookingComment
{
    public int Id { get; init; }
    public string? Username { get; init; }
    public string? AuthorName { get; set; }
    public string? Comment { get; init; }
    public string? CreatedDate { get; init; }
}

public sealed class AgentSummary
{
    public int UserId { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? UserType { get; init; }
    public string? Status { get; init; }
    public int BookingCount { get; init; }
}

public sealed class DashboardStats
{
    public int TotalBookings { get; init; }
    public int VisibleBookings { get; init; }
    public int TotalPassengers { get; init; }
    public int TotalAgents { get; init; }
    public int TotalComments { get; init; }
    public double TotalRevenue { get; init; }
    public IReadOnlyList<BookingTypeCount> BookingsByType { get; init; } = [];
    public IReadOnlyList<MonthlyBookingCount> BookingsByMonth { get; init; } = [];
}

public sealed class BookingTypeCount
{
    public string? TypeName { get; init; }
    public int Count { get; init; }
}

public sealed class BookingTypeOption
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public sealed class MonthlyBookingCount
{
    public string? Month { get; init; }
    public int Count { get; init; }
}

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
