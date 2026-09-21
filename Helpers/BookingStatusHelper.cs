namespace RushMyBookings.Crm.Helpers;

public static class BookingStatusHelper
{
    public static string TicketStatusLabel(string? code) => code switch
    {
        "1" => "Issued",
        "2" => "Cancelled",
        "3" => "Changed",
        _ => "Pending"
    };

    public static string McoStatusLabel(string? code) => code switch
    {
        "1" => "Completed",
        "6" => "Ready for Charge",
        "8" => "In Progress",
        _ => "Pending"
    };

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

    public static string FollowUpLabel(int? followUpBy, string? followUpDate)
    {
        if (followUpBy is > 0 || !string.IsNullOrWhiteSpace(followUpDate))
        {
            return "PEN";
        }

        return "-";
    }

    public static string TicketStatusClass(string? code) => code switch
    {
        "1" => "badge-success",
        "2" => "badge-danger",
        "3" => "badge-info",
        _ => "badge-warning"
    };

    public static string McoStatusClass(string? code) => code switch
    {
        "1" => "badge-success",
        "6" => "badge-info",
        "8" => "badge-info",
        _ => "badge-warning"
    };

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
