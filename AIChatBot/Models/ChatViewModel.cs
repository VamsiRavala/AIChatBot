using AIChatBot.Data.Models;

namespace AIChatBot.Models
{
    public class ChatViewModel
    {
        public string UserInput { get; set; }
        public List<ChatMessage> Messages { get; set; } = new();
    }
}
