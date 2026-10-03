using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

/// <summary>Future (or past) one-off week-off / work override for one person on one date.</summary>
public class RosterEntry
{
    public int RosterId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly RosterDate { get; set; }

    /// <summary>"WeekOff" = planned off; "Work" = must work even if usually off that weekday.</summary>
    [MaxLength(20)]
    public string EntryType { get; set; } = RosterEntryTypes.WeekOff;

    /// <summary>Optional shift for that day (otherwise use normal schedule).</summary>
    public int? ShiftId { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public int CreatedByEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }

    public ShiftTemplate? Shift { get; set; }
}

public static class RosterEntryTypes
{
    public const string WeekOff = "WeekOff";
    public const string Work = "Work";
}
