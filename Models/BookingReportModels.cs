namespace RushMyBookings.Crm.Models
{
    public sealed class ReportOption
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public sealed class BookingReportFilter
    {
        public string? AgentId { get; set; }
        public string? TicketStatus { get; set; }
        public string? McoStatus { get; set; }
        public string? McoSupplier { get; set; }
        public string? Tfn { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public sealed class BookingReportOptions
    {
        public IReadOnlyList<ReportOption> Agents { get; set; } = [];
        public IReadOnlyList<ReportOption> McoSuppliers { get; set; } = [];
        public IReadOnlyList<ReportOption> Tfns { get; set; } = [];
    }

    public sealed class BookingReportRow
    {
        public int BookId { get; set; }
        public string? ReferenceNo { get; set; }
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public double GrossMco { get; set; }
        public double NetMco { get; set; }
        public string? Currency { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedDate { get; set; }
        public string? TicketStatus { get; set; }
        public string? McoStatus { get; set; }
        public int? FollowUpBy { get; set; }
        public string? FollowUpDate { get; set; }
    }

    public sealed class BookingReportViewModel
    {
        public BookingReportFilter Filter { get; set; } = new();
        public BookingReportOptions Options { get; set; } = new();
        public IReadOnlyList<BookingReportRow> Rows { get; set; } = [];
        public int TotalCount { get; set; }
        public bool IsAgent { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Filter.PageSize));
        public int FirstRow => TotalCount == 0 ? 0 : (Filter.Page - 1) * Filter.PageSize + 1;
        public int LastRow => Math.Min(Filter.Page * Filter.PageSize, TotalCount);
    }
}
