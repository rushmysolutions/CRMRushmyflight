using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Services;

public interface ICrmDataService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<BookingSummary>> GetBookingsAsync(
        int page,
        int pageSize,
        BookingSearchFilter filter,
        IReadOnlyDictionary<int, string>? typeLookup = null,
        CancellationToken cancellationToken = default);
    Task<BookingDetail?> GetBookingByIdAsync(int bookId, CancellationToken cancellationToken = default);
    Task<CreateBookingViewModel?> GetBookingForEditAsync(int bookId, CancellationToken cancellationToken = default);
    Task<BookingCommentsViewModel?> GetBookingCommentsAsync(int bookId, CancellationToken cancellationToken = default);
    Task<bool> AddBookingCommentAsync(int bookId, string userId, string comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentSummary>> GetAgentsAsync(CancellationToken cancellationToken = default);
    Task<UserAccount?> ValidateUserAsync(string login, string password, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingTypeOption>> GetBookingTypesAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateNextReferenceNoAsync(CancellationToken cancellationToken = default);
    Task<(int BookId, string ReferenceNo)> CreateBookingAsync(CreateBookingViewModel model, string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateBookingAsync(CreateBookingViewModel model, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentPerformanceViewModel>> GetAgentPerformanceAsync(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
}
