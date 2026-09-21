namespace RushMyBookings.Crm.Models
{
    public class AgentPerformanceViewModel
    {
        public int UserId { get; set; }
        public string AgentName { get; set; }

        public decimal GrossMco { get; set; }
        public decimal NetMco { get; set; }

        public int Bookings { get; set; }
        public int GrossCalls { get; set; }
        public decimal BufferCharged { get; set; }
        public decimal NonBuffer { get; set; }
        public decimal TotalCharged { get; set; }
        public decimal Rpc { get; set; }
        public decimal ConversionRate { get; set; }
    }
}
