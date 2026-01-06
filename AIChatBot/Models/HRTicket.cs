namespace AIChatBot.Web.Models
{
    public class HRTicket
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Priority { get; set; } = "Normal";

        public string ReporterEmail { get; set; } = string.Empty;

        public string? SessionId { get; set; }

        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
