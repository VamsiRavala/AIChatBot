using System.ComponentModel.DataAnnotations;

namespace AIChatBot.Data.Models
{
    public class FAQInteractionLog
    {
        [Key]
        public int Id { get; set; }
        public string UserQuestion { get; set; }
        public string MatchedQuestion { get; set; }
        public int? FAQId { get; set; }
        public string AnswerReturned { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
