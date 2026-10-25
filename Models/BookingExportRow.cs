namespace RushMyBookings.Crm.Models
{
    // Excel export: one row per booking (names must match the SQL aliases)
    public sealed class BookingExportRow
    {
        public int BookId { get; set; }
        public string? ReferenceNo { get; set; }
        public string? TypeName { get; set; }
        public string? DropSale { get; set; }
        public string? TravelDate { get; set; }
        public string? AirlineName { get; set; }
        public string? Tfn { get; set; }
        public string? CardHolderName { get; set; }
        public string? CardType { get; set; }
        public string? CardNumber { get; set; }
        public string? ExpiryDate { get; set; }
        public string? Cvv { get; set; }
        public string? BillingPhone { get; set; }
        public string? ContactPhone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Country { get; set; }
        public string? AirlineConfirmation { get; set; }
        public string? AmadeusPnr { get; set; }
        public string? TicketStatus { get; set; }
        public string? Supplier { get; set; }
        public string? TicketCost { get; set; }
        public string? IssuanceFee { get; set; }
        public double Mco { get; set; }
        public double NetMco { get; set; }
        public double Total { get; set; }
        public string? PartialRefund { get; set; }
        public string? CreditValue { get; set; }
        public string? McoSupplierId { get; set; }
        public string? McoSupplierName { get; set; }
        public string? McoStatus { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedDate { get; set; }
    }

    // Excel export: passengers of a booking
    public sealed class PassengerExportRow
    {
        public int PassId { get; set; }
        public int BookingId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Dob { get; set; }
        public string? Gender { get; set; }
        public string? TicketNo { get; set; }
    }
}
