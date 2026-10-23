namespace RushMyBookings.Crm.Helpers;

public sealed record StatusOption(string Value, string Label);

public static class BookingStatusHelper
{
    // ---- Real codes used in tbl_book_flightdetail (same as old PHP panel) ----
    public static readonly IReadOnlyList<StatusOption> McoStatuses =
    [
        new("0",  "Pending"),
        new("1",  "Confirmed"),
        new("5",  "Refunded the MCO"),
        new("6",  "Voided the Charge"),
        new("7",  "Chargeback"),
        new("8",  "Declined"),
        new("11", "Ready for Charge")
    ];

    public static readonly IReadOnlyList<StatusOption> TicketStatuses =
    [
        new("0",  "Pending"),
        new("1",  "Confirmed"),
        new("3",  "Cancelled"),
        new("5",  "Refunded"),
        new("6",  "Voided the Charge"),
        new("7",  "Chargeback"),
        new("8",  "Declined"),
        new("10", "CCV Required"),
        new("11", "CCV DONE"),
        new("12", "CCV Failed")
    ];

    // ---- What an Agent is allowed to pick ----
    private static readonly HashSet<string> AgentMco = ["0", "1", "11"]; // Pending, Confirmed, Ready for Charge
    private static readonly HashSet<string> AgentTicket = ["0", "1", "3"];  // Pending, Confirmed, Cancelled

    /// Dropdown options. Agents get the short list + the booking's current value
    /// (so a status set by Admin, e.g. Chargeback, still shows and is not lost on save).
    public static IEnumerable<StatusOption> McoOptionsFor(bool restricted, string? current) =>
        restricted ? McoStatuses.Where(o => AgentMco.Contains(o.Value) || o.Value == current) : McoStatuses;

    public static IEnumerable<StatusOption> TicketOptionsFor(bool restricted, string? current) =>
        restricted ? TicketStatuses.Where(o => AgentTicket.Contains(o.Value) || o.Value == current) : TicketStatuses;

    /// Server-side guard for Agent saves: allowed value, or unchanged existing value; otherwise keep existing.
    public static string AgentSafeMco(string? submitted, string? existing) =>
        submitted is not null && (AgentMco.Contains(submitted) || submitted == existing) ? submitted : (existing ?? "0");

    public static string AgentSafeTicket(string? submitted, string? existing) =>
        submitted is not null && (AgentTicket.Contains(submitted) || submitted == existing) ? submitted : (existing ?? "0");

    // ---- Labels ----
    private static string Label(IReadOnlyList<StatusOption> list, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Pending";
        return list.FirstOrDefault(o => o.Value == code.Trim())?.Label ?? $"Unknown ({code})";
    }

    public static string TicketStatusLabel(string? code) => Label(TicketStatuses, code);
    public static string McoStatusLabel(string? code) => Label(McoStatuses, code);

    public static string TicketStatusClass(string? code) => code?.Trim() switch
    {
        "1" or "11" => "badge-success",
        "3" or "7" or "8" or "12" => "badge-danger",
        "5" or "6" => "badge-muted",
        "10" => "badge-info",
        _ => "badge-warning"
    };

    public static string McoStatusClass(string? code) => code?.Trim() switch
    {
        "1" => "badge-success",
        "11" => "badge-info",
        "7" or "8" => "badge-danger",
        "5" or "6" => "badge-muted",
        _ => "badge-warning"
    };

    // ---- unchanged ----
    public static string QualityStatusLabel(string? code) => code switch
    {
        "1" => "Approved",
        "2" => "Rejected",
        "Y" => "Yes",
        _ => "Pending"
    };

    public static string AckStatusLabel(int status) => status switch
    {
        1 => "Acknowledged",
        _ => "Pending"
    };

    public static string FollowUpLabel(int? followUpBy, string? followUpDate) =>
        followUpBy is > 0 || !string.IsNullOrWhiteSpace(followUpDate) ? "PEN" : "-";

    public static string QualityStatusClass(string? code) => code switch
    {
        "1" => "badge-success",
        "2" => "badge-danger",
        _ => "badge-warning"
    };

    public static string AckStatusClass(int status) => status switch
    {
        1 => "badge-success",
        _ => "badge-muted"
    };

    public static string FollowUpClass(int? followUpBy, string? followUpDate) =>
        followUpBy is > 0 || !string.IsNullOrWhiteSpace(followUpDate) ? "badge-danger" : "badge-muted";
}