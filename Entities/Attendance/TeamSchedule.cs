namespace RushMyBookings.Crm.Entities.Attendance;

/// <summary>Default shift + week-offs for a whole team.</summary>
public class TeamSchedule
{
    public int TeamId { get; set; }

    public int? ShiftId { get; set; }

    /// <summary>See <see cref="Helpers.WeekOffDays"/> bit flags. Default Sat+Sun.</summary>
    public byte WeekOffMask { get; set; } = 96; // Saturday | Sunday

    public Team? Team { get; set; }

    public ShiftTemplate? Shift { get; set; }
}
