using ClosedXML.Excel;
using RushMyBookings.Crm.Models;
using System.Drawing;
using System.Globalization;
using static RushMyBookings.Crm.Helpers.FormatHelper;

namespace RushMyBookings.Crm.Helpers
{
    public static class BookingReportExcel
    {
        private static readonly string[] Headers =
        [
            "Booking No", "Booking Type", "Is Drop-Sale", "Travelling Date", "Airline Name", "TFN",
        "Card Holder Name", "Card Type", "Credit Card No", "Expiration Date", "CVV",
        "Billing Phone No", "Contact Phone No", "Email Address", "City", "State", "Zip Code", "Country",
        "Airline Confirmation", "Amadeus PNR", "Ticket Status", "Supplier", "Ticket Cost", "Issuance Fee",
        "MCO", "Net MCO", "Total", "Credit Value", "MCO Supplier", "MCO Status", "Final Status",
        "Created By", "Created Date", "Passenger List"
        ];

        // Written as TEXT so Excel never shows 4.5235E+15 or drops leading zeros
        // (TFN, Card No, Expiry, CVV, Billing Phone, Contact Phone, Zip)
        private static readonly HashSet<int> TextColumns = [6, 9, 10, 11, 12, 13, 17];

        public static byte[] Build(IReadOnlyList<BookingExportRow> rows, IReadOnlyDictionary<int, string> passengers)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bookings");

            // Header: green background + bold
            for (var c = 0; c < Headers.Length; c++)
                ws.Cell(1, c + 1).Value = Headers[c];

            var header = ws.Range(1, 1, 1, Headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#92D050");
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var col in TextColumns)
                ws.Column(col).Style.NumberFormat.Format = "@";

            var r = 2;
            foreach (var b in rows)
            {
                var values = new object?[]
                {
                b.ReferenceNo,
                b.TypeName,
                b.DropSale == "1" ? "Yes" : "No",
                b.TravelDate,
                b.AirlineName,
                b.Tfn,
                b.CardHolderName,
                CardType(b.CardType),
                Digits(b.CardNumber),
                b.ExpiryDate?.Replace("/", "").Trim(),
                b.Cvv,
                b.BillingPhone,
                b.ContactPhone,
                b.Email,
                b.City,
                b.State,
                b.Zip,
                b.Country,
                b.AirlineConfirmation,
                b.AmadeusPnr,
                BookingStatusHelper.TicketStatusLabel(b.TicketStatus),
                b.Supplier,
                NumberOrText(b.TicketCost),
                ZeroAsBlank(b.IssuanceFee),
                b.Mco,
                b.NetMco,
                b.Total,
                NumberOrText(FirstNonEmpty(b.PartialRefund, b.CreditValue)),
                string.IsNullOrWhiteSpace(b.McoSupplierName) ? b.McoSupplierId : b.McoSupplierName,
                BookingStatusHelper.McoStatusLabel(b.McoStatus),
                FinalStatus(b.TicketStatus, b.McoStatus),
                b.CreatedByName,
                b.CreatedDate,
                passengers.TryGetValue(b.BookId, out var p) ? p : ""
                };

                for (var c = 0; c < values.Length; c++)
                {
                    var cell = ws.Cell(r, c + 1);
                    switch (values[c])
                    {
                        case null: break;
                        case double d: if (double.IsFinite(d)) cell.Value = d; break;
                        default: cell.Value = values[c]!.ToString()?.Trim() ?? ""; break;
                    }
                }
                r++;
            }

            if (rows.Count > 0)
            {
                ws.Range(2, 23, r - 1, 28).Style.NumberFormat.Format = "0.00";   // Ticket Cost .. Credit Value
                ws.Range(1, 1, r - 1, Headers.Length).SetAutoFilter();
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns(1, Headers.Length - 1).AdjustToContents();
            ws.Column(Headers.Length).Width = 70;
            ws.Column(Headers.Length).Style.Alignment.WrapText = true;
            ws.Rows(2, Math.Max(2, r - 1)).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // "1: ALDANA MOLINA - Female - Confirm - Nov 26, 1975" — one line per passenger.
        // Old PHP rows keep several passengers in ONE row separated by "~!".
        public static string FormatPassengers(IEnumerable<PassengerExportRow> rows)
        {
            var lines = new List<string>();
            var n = 1;

            foreach (var row in rows)
            {
                var first = Split(row.FirstName);
                var middle = Split(row.MiddleName);
                var last = Split(row.LastName);
                var dob = Split(row.Dob);
                var gender = Split(row.Gender);
                var ticket = Split(row.TicketNo);

                var count = new[] { first.Length, middle.Length, last.Length, dob.Length, gender.Length, ticket.Length }.Max();

                for (var i = 0; i < count; i++)
                {
                    var name = string.Join(" ", new[] { At(first, i), At(middle, i), At(last, i) }
                                                  .Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(At(ticket, i)))
                        continue;

                    lines.Add($"{n++}: {name} - {GenderName(At(gender, i))} - {At(ticket, i)} - {FormatDob(At(dob, i))}");
                }
            }

            return string.Join("\n", lines);
        }

        private static string[] Split(string? value) =>
            (value ?? "").Split("~!", StringSplitOptions.None).Select(s => s.Trim()).ToArray();

        private static string At(string[] arr, int i) => i < arr.Length ? arr[i] : "";

        private static string GenderName(string g) => g.ToUpperInvariant() switch
        {
            "M" or "MALE" => "Male",
            "F" or "FEMALE" => "Female",
            _ => g
        };

        private static readonly string[] DobFormats =
            ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "d-M-yyyy"];

        private static string FormatDob(string dob) =>
            DateTime.TryParseExact(dob, DobFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)
                : dob;

        private static string FinalStatus(string? ticket, string? mco)
        {
            var t = string.IsNullOrWhiteSpace(ticket) ? "0" : ticket.Trim();
            var m = string.IsNullOrWhiteSpace(mco) ? "0" : mco.Trim();
            return t == m ? BookingStatusHelper.TicketStatusLabel(t) : "Mis-matched";
        }

        private static string CardType(string? code)
        {
            var name = FormatHelper.CardTypeName(code);
            return name is null or "-" ? "" : name;
        }

        private static string? Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length >= 12 ? digits : value.Trim();
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v.Trim() != "0");

        private static object? NumberOrText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim();

            if (v.Equals("NAN", StringComparison.OrdinalIgnoreCase)) return null;   // old PHP placeholder -> blank

            return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d)
                ? d
                : v;
        }

        private static object? ZeroAsBlank(string? value)
        {
            var v = NumberOrText(value);
            return v is double d && d == 0 ? null : v;
        }

       
    }
}
