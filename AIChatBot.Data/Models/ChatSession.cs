using System.ComponentModel.DataAnnotations;

namespace AIChatBot.Data.Models
{
    public class ChatSession
    {
        [Key]
        public int Id { get; set; }
        public string? UserId { get; set; }
        public DateTime StartedAt { get; set; }

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        public string Sender { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string SessionId { get; set; }
        public int ChatSessionId { get; set; }
        public ChatSession ChatSession { get; set; }
        public bool IsRead { get; set; } = false;
    }
}
