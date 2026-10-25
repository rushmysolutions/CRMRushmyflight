using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;
using System.Text;

namespace RushMyBookings.Crm.Helpers
{
    public static class BookingTxtBuilder
    {
        private const string DefaultCompany = "RushMyFlight";

        public static string Build(BookingDetail d, CreateBookingViewModel e)
        {
            var sb = new StringBuilder();
            void Line(string label, string? value) =>
                sb.Append(label).Append(": ").Append(value?.Trim() ?? "").Append("\r\n");

            Line("Company Name", string.IsNullOrWhiteSpace(d.McoSupplierName) ? DefaultCompany : d.McoSupplierName);
            Line("Booking Type", d.TypeName);
            Line("Booking No", d.ReferenceNo);
            Line("Card Holder Name", d.CardHolderName);
            Line("Card Type", CardType(d.PaymentMethod));
            Line("Credit Card No", DigitsOrRaw(e.CardNumber));
            Line("Expiration Date", e.ExpiryDate?.Replace("/", "").Replace("-", "").Trim());
            Line("CVV", e.Cvv);
            Line("MCO", Money(d.McoAmount, d.Currency));
            Line("Billing Phone No", d.Phone);
            Line("Contact Phone", d.InboundPhone);
            Line("Email ID", d.Email);
            Line("Address", d.BillingAddress);
            Line("City", d.City);
            Line("State", d.State);
            Line("Zip Code", d.ZipCode);
            Line("Country", d.Country);

            return sb.ToString();
        }

        private static string CardType(string? code)
        {
            var name = FormatHelper.CardTypeName(code);
            return name is null or "-" ? "" : name;
        }

        // "4523-5000-0024-5257" -> "4523500000245257"; leaves anything without digits untouched
        private static string? DigitsOrRaw(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length >= 12 ? digits : value.Trim();
        }

        private static string Money(double amount, string? currency)
        {
            var cur = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
            var symbol = cur switch { "EUR" => "€", "GBP" => "£", _ => "$" };
            return $"{symbol}{amount:0.00} {cur}";
        }
    }
}
