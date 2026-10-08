using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Services;

public interface IAttendanceService
{
    Task EnsureDatabaseAsync();

    Task<Employee?> ValidateEmployeeLoginAsync(string login, string password);

    Task<Employee?> FindEmployeeByCrmUserAsync(int crmUserId, string? email);

    Task<Employee?> GetEmployeeAsync(int employeeId);

    Task<IReadOnlyList<EmployeeListItem>> GetEmployeesAsync(string? search = null);

    /// <summary>
    /// Light list for Monthly dropdown.
    /// SysAdmin sees everyone; Admin/HR see everyone except SystemAdministrator.
    /// </summary>
    Task<IReadOnlyList<EmployeeOption>> GetEmployeeFilterOptionsAsync(string? viewerRole);

    Task<IReadOnlyList<TeamOption>> GetTeamsAsync();

    Task<IReadOnlyList<DesignationOption>> GetDesignationsAsync();

    Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync();

    Task<IReadOnlyList<RoleOption>> GetRolesAsync();

    Task<IReadOnlyList<EmployeeOption>> GetLeadOptionsAsync();

    Task<(bool Ok, string Message, int EmployeeId)> CreateEmployeeAsync(EmployeeEditViewModel model, int? createdBy);

    Task<(bool Ok, string Message)> UpdateEmployeeAsync(EmployeeEditViewModel model);

    /// <summary>Hard-delete employee and clear related pointers. SysAdmin only at controller.</summary>
    Task<(bool Ok, string Message)> DeleteEmployeeAsync(int employeeId, int actorEmployeeId);

    /// <summary>
    /// Change password. When <paramref name="requireCurrentPassword"/> is true, current must match.
    /// </summary>
    Task<(bool Ok, string Message)> ChangePasswordAsync(
        int employeeId,
        string newPassword,
        string? currentPassword,
        bool requireCurrentPassword);

    /// <summary>Next employee code for a department, e.g. IT001, SAL002.</summary>
    Task<string?> GenerateNextEmpCodeAsync(string departmentName);

    Task<(bool Ok, string Message)> CreateTeamAsync(string name, int? leadEmployeeId);

    Task<(bool Ok, string Message)> CreateDesignationAsync(string name);

    Task<(bool Ok, string Message)> CreateDepartmentAsync(string name);

    Task<IReadOnlyList<HolidayListItem>> GetHolidaysAsync(int? year = null);

    Task<(bool Ok, string Message)> CreateHolidayAsync(DateOnly holidayDate, string name, bool isOptional);

    Task<(bool Ok, string Message)> DeleteHolidayAsync(int holidayId);

    Task<DailyAttendance?> GetTodayAttendanceAsync(int employeeId);

    /// <summary>
    /// Opt-in / opt-out for today. Pass status on opt-in (Present, WFH, HalfDay).
    /// Leave is handled by MarkOwnStatusAsync (no opt-in needed).
    /// </summary>
    Task<(bool Ok, string Message)> ToggleOptInOutAsync(int employeeId, string? status = null);

    /// <summary>Employee marks Leave (or updates status before opt-in) for today.</summary>
    Task<(bool Ok, string Message)> MarkOwnStatusAsync(int employeeId, string status);

    Task<(bool Ok, string Message)> MarkAttendanceAsync(MarkAttendanceViewModel model, int actorEmployeeId, bool isSelf);

    Task<bool> IsMonthLockedAsync(int employeeId, int year, int month);

    Task<AttendanceMonthViewModel> GetMonthViewAsync(
        int employeeId,
        int year,
        int month);

    Task<IReadOnlyList<TeamAttendanceRow>> GetTeamAttendanceAsync(
        int leadEmployeeId,
        int? teamId,
        DateOnly date,
        bool canViewAll,
        string? search = null);

    Task<IReadOnlyList<MonthlySummaryRow>> GetMonthlySummariesAsync(
        int year,
        int month,
        int? filterEmployeeId,
        int? viewerEmployeeId,
        bool canViewAll,
        bool canViewTeam,
        string? viewerRole,
        string? search = null);

    Task<(bool Ok, string Message, int Count)> CalculateMonthAsync(
        int year,
        int month,
        int calculatedByEmployeeId,
        bool lockMonth);

    /// <summary>
    /// From the 1st of each month, previous month is calculated and locked for all staff.
    /// Safe to call repeatedly (no-op if already locked).
    /// </summary>
    Task<(bool Ran, string Message)> AutoLockPreviousMonthIfDueAsync();

    Task<(bool Ok, string Message)> SetEmployeeMonthLockAsync(
        int employeeId,
        int year,
        int month,
        bool lockMonth,
        int actorEmployeeId);

    Task<bool> CanViewerAccessEmployeeAsync(
        int viewerEmployeeId,
        string? viewerRole,
        int targetEmployeeId);

    // ---- Shifts / schedules / roster ----

    Task<IReadOnlyList<ShiftListItem>> GetShiftsAsync(bool activeOnly = true);

    Task<(bool Ok, string Message)> CreateShiftAsync(string name, TimeSpan start, TimeSpan end);

    Task<(bool Ok, string Message)> UpdateShiftAsync(int shiftId, string name, TimeSpan start, TimeSpan end);

    Task<(bool Ok, string Message)> DeleteShiftAsync(int shiftId);

    Task<(bool Ok, string Message)> SetShiftActiveAsync(int shiftId, bool isActive);

    Task<ScheduleEditViewModel?> GetTeamScheduleEditAsync(int teamId);

    Task<(bool Ok, string Message)> SaveTeamScheduleAsync(ScheduleEditViewModel model);

    Task<ScheduleEditViewModel?> GetEmployeeScheduleEditAsync(int employeeId);

    Task<(bool Ok, string Message)> SaveEmployeeScheduleAsync(ScheduleEditViewModel model);

    Task<ResolvedWorkDay> ResolveWorkDayAsync(int employeeId, DateTime? at = null);

    Task<RosterWeekViewModel> GetRosterWeekAsync(
        DateOnly weekStart,
        int? teamId,
        int viewerEmployeeId,
        bool canManageAll,
        bool teamLeadOnly);

    Task<(bool Ok, string Message)> SetRosterWeekOffAsync(
        int employeeId,
        DateOnly date,
        bool isWeekOff,
        int actorEmployeeId);
}
