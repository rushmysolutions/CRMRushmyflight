using System.ComponentModel.DataAnnotations;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;

namespace RushMyBookings.Crm.ViewModels.Attendance;

public class EmployeeListItem
{
    public int EmployeeId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? TeamName { get; set; }
    public bool IsActive { get; set; }
}

public class TeamOption
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EmployeeOption
{
    public int EmployeeId { get; set; }
    public string Display { get; set; } = string.Empty;
}

public class EmployeeEditViewModel
{
    public int EmployeeId { get; set; }

    [MaxLength(30)]
    [Display(Name = "Employee Code")]
    public string EmpCode { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(80)]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [Required, EmailAddress, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required, MaxLength(80)]
    [Display(Name = "Department")]
    public string? Department { get; set; }

    [MaxLength(80)]
    public string? Designation { get; set; }

    [Required, MaxLength(40)]
    public string Username { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Required]
    [Display(Name = "Role")]
    public int RoleId { get; set; }

    [Display(Name = "Team")]
    public int? TeamId { get; set; }

    [Display(Name = "Reporting Lead")]
    public int? ReportingLeadId { get; set; }

    [Display(Name = "CRM User Id (optional)")]
    public int? CrmUserId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Join Date")]
    public DateTime JoinDate { get; set; } = DateTime.Today;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<TeamOption> Teams { get; set; } = [];
    public IReadOnlyList<EmployeeOption> Leads { get; set; } = [];
    public IReadOnlyList<DesignationOption> Designations { get; set; } = [];
    public IReadOnlyList<DepartmentOption> Departments { get; set; } = [];
    public IReadOnlyList<RoleOption> Roles { get; set; } = [];
}

public class DesignationOption
{
    public int DesignationId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DepartmentOption
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RoleOption
{
    public int RoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class HolidayListItem
{
    public int HolidayId { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
}

public class MarkAttendanceViewModel
{
    public int EmployeeId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateOnly AttendanceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    public string Status { get; set; } = DailyAttendanceStatus.Present;

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public string EmployeeName { get; set; } = string.Empty;
    public bool MonthLocked { get; set; }
    public bool HasOptedIn { get; set; }
    public bool HasOptedOut { get; set; }
    public TimeSpan? OptInTime { get; set; }
    public TimeSpan? OptOutTime { get; set; }
    public IReadOnlyList<string> StatusOptions { get; set; } = DailyAttendanceStatus.SelfSelectable;
}

public class DayAttendanceCell
{
    public DateOnly Date { get; set; }
    public string? Status { get; set; }
    public TimeSpan? OptIn { get; set; }
    public TimeSpan? OptOut { get; set; }
    public int? WorkMinutes { get; set; }
    public string? Remarks { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool HasOptedIn => OptIn.HasValue;
    public bool HasOptedOut => OptOut.HasValue;

    public string WorkHoursDisplay
    {
        get
        {
            var mins = WorkMinutes;
            if (mins is null && OptIn.HasValue && OptOut.HasValue && OptOut >= OptIn)
            {
                mins = (int)(OptOut.Value - OptIn.Value).TotalMinutes;
            }

            if (mins is null or <= 0)
            {
                return "—";
            }

            var hours = mins.Value / 60;
            var minutes = mins.Value % 60;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }
    }
}

public class AttendanceMonthViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmpCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public MonthlyAttendance? Summary { get; set; }
    public IReadOnlyList<DayAttendanceCell> Days { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool IsLocked => Summary?.Status == MonthlyAttendanceStatus.Locked;
}

public class TeamAttendanceRow
{
    public int EmployeeId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? TeamName { get; set; }
    public string? Status { get; set; }
    public TimeSpan? OptIn { get; set; }
    public TimeSpan? OptOut { get; set; }
}

public class TeamAttendanceViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? TeamId { get; set; }
    public string? Search { get; set; }
    public IReadOnlyList<TeamOption> Teams { get; set; } = [];
    public IReadOnlyList<TeamAttendanceRow> Rows { get; set; } = [];
}

public class MonthlyDayCell
{
    public int Day { get; set; }
    public string? Status { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool MissingOptOut { get; set; }
}

public class MonthlySummaryRow
{
    public int EmployeeId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? TeamName { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int HalfDays { get; set; }
    public int LeaveDays { get; set; }
    public int WfhDays { get; set; }
    public int WorkingDaysInMonth { get; set; }
    public int TotalWorkMinutes { get; set; }
    public decimal AttendancePercent { get; set; }
    public string Status { get; set; } = MonthlyAttendanceStatus.Draft;
    public IReadOnlyList<MonthlyDayCell> Days { get; set; } = [];

    public string WorkHoursDisplay
    {
        get
        {
            if (TotalWorkMinutes <= 0)
            {
                return "—";
            }

            var hours = TotalWorkMinutes / 60;
            var minutes = TotalWorkMinutes % 60;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }
    }
}

public class MonthlyReportViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public int? EmployeeId { get; set; }
    public string? Search { get; set; }
    public IReadOnlyList<EmployeeOption> Employees { get; set; } = [];
    public IReadOnlyList<MonthlySummaryRow> Rows { get; set; } = [];
    public IReadOnlyList<int> DayNumbers { get; set; } = [];
    public bool CanCloseMonth { get; set; }
}

public class CloseMonthViewModel
{
    public int Year { get; set; } = DateTime.Today.Year;
    public int Month { get; set; } = DateTime.Today.Month == 1 ? 12 : DateTime.Today.Month - 1;
    public bool LockMonth { get; set; } = true;
}

public class MyAttendanceHomeViewModel
{
    public MarkAttendanceViewModel Today { get; set; } = new();
    public AttendanceMonthViewModel CurrentMonth { get; set; } = new();
}

public class ChangePasswordViewModel
{
    /// <summary>Target employee. SysAdmin may pick anyone; others always use self.</summary>
    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string? CurrentPassword { get; set; }

    [Required]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool CanChangeAnyone { get; set; }
    public bool ChangingOwn { get; set; } = true;
    public string? TargetDisplay { get; set; }
    public IReadOnlyList<EmployeeOption> Employees { get; set; } = [];
}

public class ShiftListItem
{
    public int ShiftId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public string TimingLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ScheduleEditViewModel
{
    public int? TeamId { get; set; }
    public int? EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? ShiftId { get; set; }
    public bool InheritShift { get; set; }
    public bool InheritWeekOff { get; set; }
    public bool OffMon { get; set; }
    public bool OffTue { get; set; }
    public bool OffWed { get; set; }
    public bool OffThu { get; set; }
    public bool OffFri { get; set; }
    public bool OffSat { get; set; } = true;
    public bool OffSun { get; set; } = true;
    public IReadOnlyList<ShiftListItem> Shifts { get; set; } = [];
}

public class RosterWeekViewModel
{
    public DateOnly WeekStart { get; set; }
    public int? TeamId { get; set; }
    public IReadOnlyList<TeamOption> Teams { get; set; } = [];
    public IReadOnlyList<DateOnly> Days { get; set; } = [];
    public IReadOnlyList<RosterEmployeeRow> Rows { get; set; } = [];
    public bool CanEdit { get; set; }
}

public class RosterEmployeeRow
{
    public int EmployeeId { get; set; }
    public string EmpCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? TeamName { get; set; }
    public string ShiftLabel { get; set; } = "—";
    public string DefaultWeekOffs { get; set; } = string.Empty;
    public IReadOnlyList<RosterDayCell> Days { get; set; } = [];
}

public class RosterDayCell
{
    public DateOnly Date { get; set; }
    public bool IsWeekOff { get; set; }
    public bool FromRoster { get; set; }
    public bool ForcedWork { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Resolved shift + week-off for one employee at a point in time.</summary>
public class ResolvedWorkDay
{
    public DateOnly AttendanceDate { get; set; }
    public string? ShiftName { get; set; }
    public TimeSpan? ShiftStart { get; set; }
    public TimeSpan? ShiftEnd { get; set; }
    public bool IsWeekOff { get; set; }
    public byte WeekOffMask { get; set; } = WeekOffDays.Weekend;
    public bool HasShift => ShiftStart.HasValue && ShiftEnd.HasValue;
}
