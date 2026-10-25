namespace RushMyBookings.Crm.ViewModels
{
    public class McoSupplierListItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string? FromEmail { get; init; }
        public bool IsActive { get; init; }
    }
}
