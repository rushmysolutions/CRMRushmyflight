using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class BookingCommentsViewModel
{
    public int BookId { get; init; }
    public string? ReferenceNo { get; init; }
    public string? CustomerName { get; init; }
    public IReadOnlyList<BookingComment> Comments { get; init; } = [];
    public string? NewComment { get; set; }
    public int ReturnPage { get; set; } = 1;
    public string? ReturnSearch { get; set; }
    public string? ReturnType { get; set; }
}
