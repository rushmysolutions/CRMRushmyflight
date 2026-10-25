namespace RushMyBookings.Crm.Models
{
    public sealed class AgentSaveResult
    {
        public bool Success { get; init; }
        public int UserId { get; init; }
        public bool Created { get; init; }
        public string? GeneratedPassword { get; init; }
        public string? ErrorField { get; init; }
        public string? Error { get; init; }

        public static AgentSaveResult Fail(string field, string error) =>
            new() { Success = false, ErrorField = field, Error = error };
    }
}
