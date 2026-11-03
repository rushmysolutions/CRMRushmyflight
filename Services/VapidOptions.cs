namespace RushMyBookings.Crm.Services;

public class VapidOptions
{
    public const string SectionName = "Vapid";

    public string Subject { get; set; } = "mailto:admin@rushmybookings.local";
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>How many minutes before shift start to ping (default 5).</summary>
    public int MinutesBeforeShift { get; set; } = 5;

    public bool Enabled { get; set; } = true;
}
