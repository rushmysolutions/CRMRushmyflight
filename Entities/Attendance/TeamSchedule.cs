namespace RushMyBookings.Crm.Entities.Attendance;

/// <summary>Default shift + week-offs for a whole team.</summary>
public class TeamSchedule
{
    public int TeamId { get; set; }

    public int? ShiftId { get; set; }

    /// <summary>
    /// True = no fixed shift timing. Team members can opt in / opt out at any time
    /// (week-offs and locked months still apply). ShiftId is ignored when this is on.
    /// Only System Administrator / Administrator may turn this on or off.
    /// </summary>
    public bool IsFlexibleTiming { get; set; }

    /// <summary>See <see cref="Helpers.WeekOffDays"/> bit flags. Default Sat+Sun.</summary>
    public byte WeekOffMask { get; set; } = 96; // Saturday | Sunday

    /// <summary>
    /// When true, a punch from 12:00 AM until 4:00 AM is saved on the previous day.
    /// Overnight shifts (end before start) still follow their own end time.
    /// </summary>
    public bool EarlyMorningPreviousDay { get; set; }

    public Team? Team { get; set; }

    public ShiftTemplate? Shift { get; set; }
}
