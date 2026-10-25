using RushMyBookings.Crm.Entities;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;
using static RushMyBookings.Crm.Models.UserAccount;

namespace RushMyBookings.Crm.Services;

public interface ICrmDataService
{
    Task<bool> CanConnectAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<PagedResult<BookingSummary>> GetBookingsAsync(
        int page,
        int pageSize,
        BookingSearchFilter filter,
        IReadOnlyDictionary<int, string>? typeLookup = null, BookingAccessScope? scope = null);
    Task<BookingDetail?> GetBookingByIdAsync(int bookId);
    Task<CreateBookingViewModel?> GetBookingForEditAsync(int bookId);
    Task<BookingCommentsViewModel?> GetBookingCommentsAsync(int bookId);
    Task<bool> AddBookingCommentAsync(int bookId, string userId, string comment);
    Task<IReadOnlyList<AgentSummary>> GetAgentsAsync();
    Task<LoginResult> ValidateUserAsync(string login, string password, string? clientIp);
    Task UpdateLastLoginAsync(int userId);
    Task<IReadOnlyList<BookingTypeOption>> GetBookingTypesAsync();
    Task<string> GenerateNextReferenceNoAsync();
    Task<(int BookId, string ReferenceNo)> CreateBookingAsync(CreateBookingViewModel model, string userId);
    Task<bool> UpdateBookingAsync(CreateBookingViewModel model, string userId);
    Task<IReadOnlyList<AgentPerformanceViewModel>> GetAgentPerformanceAsync(
        DateTime? fromDate,
        DateTime? toDate, int? onlyUserId = null);

    Task<IReadOnlyList<Tfn>> GetTfnsAsync();
    Task<Tfn?> GetTfnAsync(int id);
    Task<bool> TfnExistsAsync(string tfnNumber, int excludeId = 0);
    Task SaveTfnAsync(Tfn tfn);
    Task DeleteTfnAsync(int id);

    Task<IReadOnlyList<Tfn>> GetActiveTfnsAsync();

    Task<(IReadOnlyList<AgentListItem> Items, int Total)> SearchAgentsAsync(
    string? name, string? phone, string? email, int page, int pageSize, int? onlyUserId = null);

    Task<AgentFormViewModel?> GetAgentFormAsync(int id);
    Task<Dictionary<string, string>> ValidateAgentAsync(AgentFormViewModel model);
    //Task SaveAgentAsync(AgentFormViewModel model);
    Task ToggleAgentStatusAsync(int id);
    Task<AgentDetailsViewModel?> GetAgentDetailsAsync(int id);
    Task<string?> GetAgentUsernameAsync(int id);
    Task<bool> ChangeAgentPasswordAsync(int id, string newPassword);
    //Task<IReadOnlyList<McoSupplierListItem>> GetMcoSuppliersAsync();
    Task DeleteMcoSupplierAsync(int id);
    Task ToggleMcoSupplierStatusAsync(int id);

    Task<McoSupplierFormViewModel?> GetMcoSupplierFormAsync(int id);
    Task<bool> UpdateMcoSupplierAsync(McoSupplierFormViewModel model);
    public   Task<IReadOnlyList<McoSupplierOption>> GetMcoSuppliersAsync();

    Task<IReadOnlyList<McoSupplierOption>> GetMcoSupplierOptionsAsync();

    Task<McoSupplier?> GetMcoSupplierForMailAsync(int id);
    Task<bool> AcknowledgeBookingAsync(int bookId, string? ip, string? location);
    Task<AgentSaveResult> SaveAgentAsync(AgentFormViewModel model);

    Task<IReadOnlyList<LatestCommentItem>> GetLatestCommentsAsync(int hours, int? onlyUserId = null);
    Task<(bool Exists, string? CreatedBy)> GetBookingOwnerAsync(int bookId);
    Task<(string? Ticket, string? Mco)?> GetBookingStatusesAsync(int? bookId);

    Task<BookingReportOptions> GetBookingReportOptionsAsync();
    Task<(IReadOnlyList<BookingReportRow> Rows, int Total)> GetBookingReportAsync(BookingReportFilter filter, int? onlyUserId);


    Task<(IReadOnlyList<BookingExportRow> Rows, IReadOnlyDictionary<int, string> Passengers)>
        GetBookingReportExportAsync(BookingReportFilter filter, int maxRows = 20000);
    Task<ManageIpViewModel?> GetAgentIpsAsync(int userId);
    Task<bool> UpdateAgentIpsAsync(int userId, string allowedIps);
    Task<int> UpdateAllAgentIpsAsync(string allowedIps);
}
