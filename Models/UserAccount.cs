namespace RushMyBookings.Crm.Models;

public sealed class UserAccount
{
    public int UserId { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? UserType { get; init; }
    public string DisplayName =>
        string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
        is { Length: > 0 } name ? name : Username ?? Email ?? "User";
    public enum LoginStatus { Success, InvalidCredentials, IpNotAllowed }

    public sealed record LoginResult(LoginStatus Status, UserAccount? User = null);
}
