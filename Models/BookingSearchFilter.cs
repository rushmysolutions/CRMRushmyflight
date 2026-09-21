namespace RushMyBookings.Crm.Models;

public sealed class BookingSearchFilter
{
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public string? BookingNo { get; init; }
    public string? AirlineConfirmation { get; init; }
    public string? LastFourCc { get; init; }
    public string? DateFrom { get; init; }
    public string? DateTo { get; init; }
    public string? Type { get; init; }
}
