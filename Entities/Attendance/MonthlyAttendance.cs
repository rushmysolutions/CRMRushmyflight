using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class MonthlyAttendance
{
    public int MonthlyId { get; set; }

    public int EmployeeId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public int WorkingDaysInMonth { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public int HalfDays { get; set; }

    public int LeaveDays { get; set; }

    public int WfhDays { get; set; }

    public int HolidayDays { get; set; }

    public int TotalWorkMinutes { get; set; }

    public decimal AttendancePercent { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = MonthlyAttendanceStatus.Draft;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    public int? CalculatedByEmployeeId { get; set; }

    public Employee? Employee { get; set; }
}
