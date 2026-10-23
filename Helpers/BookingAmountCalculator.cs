using RushMyBookings.Crm.ViewModels;
using System.Globalization;

namespace RushMyBookings.Crm.Helpers
{
    public static class BookingAmountCalculator
    {
        // Change here if the deduction ever changes; the view reads it too
        public const int McoDeductionPercent = 10;

        private const NumberStyles Styles =
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

        public static decimal Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0m;
            var cleaned = value.Replace("$", "").Trim();
            return decimal.TryParse(cleaned, Styles, CultureInfo.InvariantCulture, out var n) ? n : 0m;
        }

        // "400, 250" -> 650
        public static decimal SumList(string? values) =>
            string.IsNullOrWhiteSpace(values)
                ? 0m
                : values.Split(',', StringSplitOptions.RemoveEmptyEntries).Sum(Parse);

        public static (decimal Total, decimal NetMco) Calculate(string? ticketCost, string? mcoAmount)
        {
            var ticket = SumList(ticketCost);
            var mco = Parse(mcoAmount);
            var deduction = mco * McoDeductionPercent / 100m;

            return (Round(ticket + mco), Round(mco - deduction));
        }

        // Writes the calculated values back into the view model (for redisplay + save)
        public static void Apply(CreateBookingViewModel model)
        {
            var (total, net) = Calculate(model.TicketCost, model.McoAmount);
            model.TotalAmount = total.ToString("0.00", CultureInfo.InvariantCulture);
            model.NetMco = net.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}
