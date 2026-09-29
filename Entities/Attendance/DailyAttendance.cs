using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class DailyAttendance
{
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    /// <summary>Time of day when the employee opted in (stored in CheckInTime column).</summary>
    public TimeSpan? OptInTime { get; set; }

    /// <summary>Time of day when the employee opted out (stored in CheckOutTime column).</summary>
    public TimeSpan? OptOutTime { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = DailyAttendanceStatus.Present;

    public int? WorkMinutes { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public int MarkedByEmployeeId { get; set; }

    public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Source { get; set; } = "Self";

    public Employee? Employee { get; set; }

    public bool HasOptedIn => OptInTime.HasValue;
    public bool HasOptedOut => OptOutTime.HasValue;
}
