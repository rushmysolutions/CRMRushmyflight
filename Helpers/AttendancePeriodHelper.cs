namespace RushMyBookings.Crm.Helpers;

/// <summary>
/// Month filters only allow the current month and the previous month (no older years).
/// </summary>
public static class AttendancePeriodHelper
{
    public const int VisibleMonthCount = 2;

    public static IReadOnlyList<DateTime> GetAllowedMonths(DateTime? asOf = null)
    {
        var today = (asOf ?? DateTime.Today).Date;
        var list = new List<DateTime>(VisibleMonthCount);
        for (var i = 0; i < VisibleMonthCount; i++)
        {
            var d = today.AddMonths(-i);
            list.Add(new DateTime(d.Year, d.Month, 1));
        }

        return list;
    }

    public static (int Year, int Month) Clamp(int? year, int? month)
    {
        var allowed = GetAllowedMonths();
        if (year is null || month is null)
        {
            var current = allowed[0];
            return (current.Year, current.Month);
        }

        var match = allowed.FirstOrDefault(p => p.Year == year && p.Month == month);
        if (match != default)
        {
            return (match.Year, match.Month);
        }

        var fallback = allowed[0];
        return (fallback.Year, fallback.Month);
    }

    public static bool IsAllowed(int year, int month) =>
        GetAllowedMonths().Any(p => p.Year == year && p.Month == month);
}
