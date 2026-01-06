using System.ComponentModel.DataAnnotations.Schema;

namespace AIChatBot.Models
{
    public class HRFAQ
    {
        public int Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public string Category { get; set; }
        
        public float[]? Embedding { get; set; }
    }
}
