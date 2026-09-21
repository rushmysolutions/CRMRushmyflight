using System.Globalization;

namespace RushMyBookings.Crm.Helpers;

public static class FormatHelper
{
    public static string Money(double amount, string? currency = "USD")
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var formatted = amount.ToString("N2", culture);
        return string.IsNullOrWhiteSpace(currency) || currency == "USD"
            ? $"${formatted}"
            : $"{currency} {formatted}";
    }

    public static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static string CardTypeName(string? code) => code switch
    {
        "1" => "Visa",
        "2" => "Mastercard",
        "3" => "American Express",
        "4" => "Discover",
        _ => Display(code)
    };

    public static string LanguageName(int lang) => lang switch
    {
        1 => "Spanish",
        _ => "English"
    };

    public static string MaskCard(string? cardNumber, string? lastFour)
    {
        if (!string.IsNullOrWhiteSpace(lastFour))
        {
            return $"••••-••••-••••-{lastFour}";
        }

        return string.IsNullOrWhiteSpace(cardNumber) ? "-" : "••••-••••-••••-••••";
    }

    public static string AttachmentDisplayName(string fileName)
    {
        var rndIndex = fileName.IndexOf("rnd", StringComparison.OrdinalIgnoreCase);
        return rndIndex >= 0 ? fileName[(rndIndex + 3)..] : fileName;
    }
}
