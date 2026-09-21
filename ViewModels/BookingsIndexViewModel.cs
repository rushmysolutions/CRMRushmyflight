using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class BookingsIndexViewModel
{
    public PagedResult<BookingSummary> Bookings { get; init; } = new() { Items = [] };
    public IReadOnlyList<BookingTypeOption> BookingTypes { get; init; } = [];
    public BookingSearchFilter Filter { get; init; } = new();
    public int Page { get; init; } = 1;
}
