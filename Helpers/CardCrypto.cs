using System.Security.Cryptography;
using System.Text;

namespace RushMyBookings.Crm.Helpers;

/// <summary>
/// Matches legacy PHP booking crypto:
///   $ciphering = "AES-128-CTR";
///   $decryption_iv = '1234567891012345';
///   $decryption_key = "GurujiGuruji";
///   $decryption = openssl_decrypt($ccnumbernew, $ciphering, $decryption_key, $options, $decryption_iv);
///   $ccnumbernew = base64_decode($decryption);
/// CVV uses openssl_decrypt only (no second base64_decode).
/// </summary>
public static class CardCrypto
{
    private const string CipherKey = "GurujiGuruji";
    private const string CipherIv = "1234567891012345";

    public static string? DecryptCardNumber(string? stored)
    {
        var plain = DecryptOpenSsl(stored);
        if (plain is null)
        {
            return PassThroughIfLooksPlain(stored);
        }

        // PHP: $ccnumbernew = base64_decode($decryption);
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(plain.Trim()));
            return string.IsNullOrWhiteSpace(decoded) ? plain : decoded;
        }
        catch (FormatException)
        {
            // Already plain after decrypt (or not base64) — use as-is.
            return plain;
        }
    }

    public static string? DecryptCvv(string? stored)
    {
        var plain = DecryptOpenSsl(stored);
        return plain ?? PassThroughIfLooksPlain(stored);
    }

    /// <summary>
    /// openssl_decrypt(..., AES-128-CTR, key, options=0, iv) — ciphertext is base64 by default.
    /// </summary>
    private static string? DecryptOpenSsl(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        var trimmed = stored.Trim();

        // Already looks like a usable card/CVV — skip crypto (CRM-created plaintext rows).
        if (LooksLikePlainSensitive(trimmed))
        {
            return trimmed;
        }

        var key = BuildAes128Key(CipherKey);
        var iv = Encoding.UTF8.GetBytes(CipherIv);
        if (iv.Length != 16)
        {
            throw new InvalidOperationException("AES-128-CTR IV must be 16 bytes.");
        }

        // Prefer PHP default options=0 (base64 ciphertext). Fall back to raw bytes.
        if (TryDecrypt(trimmed, key, iv, base64Ciphertext: true, out var plain) ||
            TryDecrypt(trimmed, key, iv, base64Ciphertext: false, out plain))
        {
            return plain;
        }

        return null;
    }

    private static bool TryDecrypt(
        string stored,
        byte[] key,
        byte[] iv,
        bool base64Ciphertext,
        out string? plain)
    {
        plain = null;
        try
        {
            byte[] cipherBytes;
            if (base64Ciphertext)
            {
                cipherBytes = Convert.FromBase64String(stored);
            }
            else
            {
                cipherBytes = Encoding.UTF8.GetBytes(stored);
            }

            if (cipherBytes.Length == 0)
            {
                return false;
            }

            var plainBytes = AesCtrXor(key, iv, cipherBytes);
            plain = Encoding.UTF8.GetString(plainBytes).TrimEnd('\0').Trim();
            return !string.IsNullOrEmpty(plain);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>AES-128 in CTR mode (OpenSSL-compatible big-endian counter).</summary>
    private static byte[] AesCtrXor(byte[] key, byte[] iv, byte[] input)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 128;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        var output = new byte[input.Length];
        var counter = (byte[])iv.Clone();
        var keyStream = new byte[16];
        var offset = 0;

        while (offset < input.Length)
        {
            encryptor.TransformBlock(counter, 0, 16, keyStream, 0);
            var block = Math.Min(16, input.Length - offset);
            for (var i = 0; i < block; i++)
            {
                output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);
            }

            offset += block;
            IncrementCounter(counter);
        }

        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (var i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }

    /// <summary>PHP/OpenSSL pads short AES-128 keys with NUL bytes to 16.</summary>
    private static byte[] BuildAes128Key(string key)
    {
        var keyBytes = new byte[16];
        var src = Encoding.UTF8.GetBytes(key);
        Buffer.BlockCopy(src, 0, keyBytes, 0, Math.Min(src.Length, 16));
        return keyBytes;
    }

    private static string? PassThroughIfLooksPlain(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? stored : stored.Trim();

    private static bool LooksLikePlainSensitive(string value)
    {
        var digits = value.Where(char.IsDigit).ToArray();
        // CVV (3–4) or PAN (12–19), allowing spaces/dashes in the original.
        return digits.Length is >= 3 and <= 19 &&
               value.All(c => char.IsDigit(c) || c is '-' or ' ');
    }
}
