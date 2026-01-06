using System.ComponentModel.DataAnnotations;

namespace AIChatBot.Data.Models
{
    public class ChatVisitor
    {
        [Key]
        public int Id { get; set; }
        public string SessionId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
