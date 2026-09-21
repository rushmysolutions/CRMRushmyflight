using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class AgentsIndexViewModel
{
    public IReadOnlyList<AgentSummary> Agents { get; init; } = [];
}
