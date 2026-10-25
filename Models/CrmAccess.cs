using System.Security.Claims;

namespace RushMyBookings.Crm.Models
{
    public static class CrmRoles
    {
        public const string SuperAdmin = "superadmin";
        public const string Admin = "Admin";
        public const string Agent = "Agent";
    }

    public static class CrmAccess
    {
        public static string? Role(ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Role)?.Value;

        public static bool IsSuperAdmin(ClaimsPrincipal user) =>
            string.Equals(Role(user), CrmRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

        public static bool IsAdminOrAbove(ClaimsPrincipal user) =>
            IsSuperAdmin(user) ||
            string.Equals(Role(user), CrmRoles.Admin, StringComparison.OrdinalIgnoreCase);

        // Anything that is not Admin/superadmin is restricted (safe default)
        public static bool IsRestricted(ClaimsPrincipal user) => !IsAdminOrAbove(user);

        public static string? UserId(ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public sealed class BookingAccessScope
    {
        public bool OwnOnly { get; init; }
        public string? UserId { get; init; }

        public static BookingAccessScope For(ClaimsPrincipal user) => new()
        {
            OwnOnly = CrmAccess.IsRestricted(user),
            UserId = CrmAccess.UserId(user)
        };
    }
}
