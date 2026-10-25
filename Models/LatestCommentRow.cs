namespace RushMyBookings.Crm.Models
{
    public sealed class LatestCommentRow
    {
        public int Cid { get; set; }
        public string BookingId { get; set; } = "";
        public string? UserId { get; set; }
        public string? CommentText { get; set; }
        public string? CreatedDate { get; set; }
    }

    public sealed class LatestCommentItem
    {
        public int BookId { get; set; }
        public string? ReferenceNo { get; set; }
        public string? CustomerName { get; set; }
        public string? Comment { get; set; }
        public string? BookingCreatedBy { get; set; }
        public string? CommentCreatedBy { get; set; }
        public string CreatedDate { get; set; } = "";
    }
}
