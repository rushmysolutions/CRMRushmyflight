using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Models
{
    public class AgentDetailsViewModel
    {
        public AgentFormViewModel Agent { get; init; } = new();
        public string? Password { get; init; }
        public IReadOnlyList<string> AllowedIps { get; init; } = [];
        public string CreatedDate { get; init; } = "";
        public bool IsActive { get; init; }
    }
}
