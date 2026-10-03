namespace RushMyBookings.Crm.Helpers;

/// <summary>
/// Week-off days stored as a small bit mask (easy to save in one column).
/// Monday = 1, Tuesday = 2, ... Sunday = 64.
/// </summary>
public static class WeekOffDays
{
    public const byte Monday = 1;
    public const byte Tuesday = 2;
    public const byte Wednesday = 4;
    public const byte Thursday = 8;
    public const byte Friday = 16;
    public const byte Saturday = 32;
    public const byte Sunday = 64;

    /// <summary>Default when no team/employee schedule is set.</summary>
    public const byte Weekend = Saturday | Sunday;

    public static byte ForDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        DayOfWeek.Sunday => Sunday,
        _ => 0
    };

    public static bool IsOff(byte mask, DayOfWeek day) =>
        (mask & ForDay(day)) != 0;

    public static byte FromFlags(
        bool mon, bool tue, bool wed, bool thu, bool fri, bool sat, bool sun)
    {
        byte m = 0;
        if (mon) m |= Monday;
        if (tue) m |= Tuesday;
        if (wed) m |= Wednesday;
        if (thu) m |= Thursday;
        if (fri) m |= Friday;
        if (sat) m |= Saturday;
        if (sun) m |= Sunday;
        return m;
    }

    public static string Describe(byte mask)
    {
        if (mask == 0)
        {
            return "None";
        }

        var parts = new List<string>();
        if ((mask & Monday) != 0) parts.Add("Mon");
        if ((mask & Tuesday) != 0) parts.Add("Tue");
        if ((mask & Wednesday) != 0) parts.Add("Wed");
        if ((mask & Thursday) != 0) parts.Add("Thu");
        if ((mask & Friday) != 0) parts.Add("Fri");
        if ((mask & Saturday) != 0) parts.Add("Sat");
        if ((mask & Sunday) != 0) parts.Add("Sun");
        return string.Join(", ", parts);
    }
}
