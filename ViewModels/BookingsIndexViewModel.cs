using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class BookingsIndexViewModel
{
    public PagedResult<BookingSummary> Bookings { get; init; } = new() { Items = [] };
    public IReadOnlyList<BookingTypeOption> BookingTypes { get; init; } = [];
    public BookingSearchFilter Filter { get; init; } = new();
    public int Page { get; init; } = 1;
    public IReadOnlyList<UserOption> Agents { get; set; } = new List<UserOption>();
    public IReadOnlyList<UserOption> Admins { get; set; } = new List<UserOption>();
    public IReadOnlyList<QualityStatusOption> QualityStatuses { get; set; } = new List<QualityStatusOption>
    {
        new() { Value = 0, Label = "Pen" },
        new() { Value = 1, Label = "App" },
        new() { Value = 2, Label = "Dec" }
    };
}
