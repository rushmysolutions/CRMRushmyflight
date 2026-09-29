using System.Security.Cryptography;
using System.Text;

namespace RushMyBookings.Crm.Services;

/// <summary>
/// Attendance module passwords — SHA256 hex (separate from legacy CRM SHA1).
/// </summary>
public static class AttendancePasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        return string.Equals(Hash(password), storedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
