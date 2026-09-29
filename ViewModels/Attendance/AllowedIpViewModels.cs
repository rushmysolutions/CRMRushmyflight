namespace RushMyBookings.Crm.ViewModels.Attendance;

public class AllowedIpListItem
{
    public int AllowedIpId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AllowedIpIndexViewModel
{
    public string? CurrentIp { get; set; }
    public IReadOnlyList<AllowedIpListItem> Items { get; set; } = [];
    public bool RestrictionActive => Items.Count > 0;
}
