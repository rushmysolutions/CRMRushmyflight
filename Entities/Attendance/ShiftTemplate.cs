using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

/// <summary>Reusable shift timing, e.g. Day 10:00–19:00 or Night 22:00–07:00.</summary>
public class ShiftTemplate
{
    public int ShiftId { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>When the shift starts (attendance date begins here).</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>When the shift ends / cutoff (opt-in closes here).</summary>
    public TimeSpan EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool CrossesMidnight => EndTime < StartTime;
}
