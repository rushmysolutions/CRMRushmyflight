using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Services;

public interface ICrmDataService
{
    Task<bool> CanConnectAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<PagedResult<BookingSummary>> GetBookingsAsync(
        int page,
        int pageSize,
        BookingSearchFilter filter,
        IReadOnlyDictionary<int, string>? typeLookup = null);
    Task<BookingDetail?> GetBookingByIdAsync(int bookId);
    Task<CreateBookingViewModel?> GetBookingForEditAsync(int bookId);
    Task<BookingCommentsViewModel?> GetBookingCommentsAsync(int bookId);
    Task<bool> AddBookingCommentAsync(int bookId, string userId, string comment);
    Task<IReadOnlyList<AgentSummary>> GetAgentsAsync();
    Task<UserAccount?> ValidateUserAsync(string login, string password);
    Task UpdateLastLoginAsync(int userId);
    Task<IReadOnlyList<BookingTypeOption>> GetBookingTypesAsync();
    Task<string> GenerateNextReferenceNoAsync();
    Task<(int BookId, string ReferenceNo)> CreateBookingAsync(CreateBookingViewModel model, string userId);
    Task<bool> UpdateBookingAsync(CreateBookingViewModel model, string userId);
    Task<IReadOnlyList<AgentPerformanceViewModel>> GetAgentPerformanceAsync(
        DateTime? fromDate,
        DateTime? toDate);
}
