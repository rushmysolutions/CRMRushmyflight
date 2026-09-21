using System.Security.Cryptography;
using System.Text;

namespace RushMyBookings.Crm.Services;

/// <summary>
/// Matches legacy CRM storage: SHA1 hash as lowercase hex string.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
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
