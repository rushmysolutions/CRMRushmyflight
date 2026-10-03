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
    public static DateOnly GetAttendanceDate(DateTime now, TimeSpan? shiftStart, TimeSpan? shiftEnd)
    {
        if (shiftStart is null || shiftEnd is null)
        {
            return DateOnly.FromDateTime(now);
        }

        var start = shiftStart.Value;
        var end = shiftEnd.Value;
        var crossesMidnight = end < start;
        var clock = now.TimeOfDay;
        var today = DateOnly.FromDateTime(now);

        if (!crossesMidnight)
        {
            return today;
        }

        // Still in the early-morning part of last night's shift → previous calendar day.
        if (clock < end)
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
        TimeSpan shiftEnd)
    {
        var crossesMidnight = shiftEnd < shiftStart;
        var windowStart = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(shiftStart));
        var windowEnd = crossesMidnight
            ? attendanceDate.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(shiftEnd))
            : attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(shiftEnd));

        return now >= windowStart && now <= windowEnd;
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
