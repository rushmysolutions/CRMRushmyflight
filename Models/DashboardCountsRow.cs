namespace RushMyBookings.Crm.Models;

internal sealed class DashboardCountsRow
{
    public int TotalBookings { get; set; }
    public int VisibleBookings { get; set; }
    public double TotalRevenue { get; set; }
    public int TotalPassengers { get; set; }
    public int TotalAgents { get; set; }
    public int TotalComments { get; set; }
}
