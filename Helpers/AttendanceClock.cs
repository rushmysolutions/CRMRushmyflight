namespace RushMyBookings.Crm.Helpers;

/// <summary>
/// Figures out "which attendance day am I on?" for day and night shifts.
///
/// Rules we use (travel sales friendly):
/// - Night shift attendance date = the day the shift STARTED (not the morning it ends).
/// - Agents may opt in after midnight until the shift EndTime (cutoff).
/// - Example night 22:00–07:00: Mon 22:00 → Tue 07:00 all counts as Monday.
/// </summary>
public static class AttendanceClock
{
    /// <summary>Optional team rule: punches before this time belong to the previous day.</summary>
    public static readonly TimeSpan EarlyMorningCutoff = TimeSpan.FromHours(4);

    public static DateOnly GetAttendanceDate(
        DateTime now,
        TimeSpan? shiftStart,
        TimeSpan? shiftEnd,
        bool earlyMorningPreviousDay = false)
    {
        var today = DateOnly.FromDateTime(now);
        var clock = now.TimeOfDay;

        if (shiftStart is null || shiftEnd is null)
        {
            if (earlyMorningPreviousDay && clock < EarlyMorningCutoff)
            {
                return today.AddDays(-1);
            }

            return today;
        }

        var end = shiftEnd.Value;
        var crossesMidnight = end < shiftStart.Value;

        // Night shift 22:00–07:00: still Monday until 07:00, even after 4:00 AM.
        if (crossesMidnight && clock < end)
        {
            return today.AddDays(-1);
        }

        // Team option: 12:00 AM–3:59 AM counts as yesterday (midnight-start shifts).
        if (earlyMorningPreviousDay && !crossesMidnight && clock < EarlyMorningCutoff)
        {
            return today.AddDays(-1);
        }

        return today;
    }

    /// <summary>
    /// Opt-in / opt-out allowed from shift Start until End (cutoff),
    /// including after midnight for overnight shifts.
    /// </summary>
    public static bool IsWithinShiftWindow(
        DateTime now,
        DateOnly attendanceDate,
        TimeSpan shiftStart,
        TimeSpan shiftEnd,
        bool earlyMorningPreviousDay = false)
    {
        var crossesMidnight = shiftEnd < shiftStart;
        var windowStart = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(shiftStart));
        var windowEnd = crossesMidnight
            ? attendanceDate.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(shiftEnd))
            : attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(shiftEnd));

        if (now >= windowStart && now <= windowEnd)
        {
            return true;
        }

        // Same shift hours on the next morning, but only until 4:00 AM,
        // because that punch is stored on this attendance date.
        if (!earlyMorningPreviousDay || crossesMidnight)
        {
            return false;
        }

        var clock = now.TimeOfDay;
        var nextMidnight = attendanceDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var cutoff = nextMidnight.Add(EarlyMorningCutoff);
        if (now < nextMidnight || now >= cutoff)
        {
            return false;
        }

        return clock >= shiftStart && clock <= shiftEnd;
    }

    /// <summary>
    /// Work minutes when opt-out can be "earlier" on the clock (overnight).
    /// </summary>
    public static int CalcWorkMinutes(TimeSpan optIn, TimeSpan optOut)
    {
        if (optOut >= optIn)
        {
            return (int)(optOut - optIn).TotalMinutes;
        }

        return (int)(TimeSpan.FromHours(24) - optIn + optOut).TotalMinutes;
    }

    public static string FormatShift(TimeSpan start, TimeSpan end)
    {
        var label = $"{FormatTime(start)} – {FormatTime(end)}";
        return end < start ? label + " (night)" : label;
    }

    public static string FormatTime(TimeSpan t) =>
        DateTime.Today.Add(t).ToString("HH:mm");
}
