namespace RushMyBookings.Crm.Entities
{
    public class McoSupplier
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? TicketCharge { get; set; }
        public string? TollFree { get; set; }
        public string? Email { get; set; }
        public string? FromEmail { get; set; }
        public string? FromSmtpPass { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 1;
    }
}
