using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardStats Stats { get; init; } = new();
    public bool DatabaseConnected { get; init; }
}
