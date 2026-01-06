using AIChatBot.Data;
using AIChatBot.Data.Models;
using AIChatBot.Models;
using AIChatBot.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIChatBot.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;

        public AdminController(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        public IActionResult AdminChat()
        {
            var visitors = _db.ChatVisitors.OrderByDescending(v => v.StartedAt).ToList();
            return View(visitors);
        }

        [HttpGet]
        public IActionResult GetChatMessages(string sessionId)
        {
            var messages = _db.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Sender,
                    m.Message,
                    m.Timestamp
                }).ToList();

            return Json(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendAdminMessage(string sessionId, string message)
        {
            ChatSession session = _db.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefault(s => s.UserId == sessionId);
            var msg = new ChatMessage
            {
                SessionId = sessionId,
                Sender = "Admin",
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsRead = true,
                ChatSession = session
            };

            _db.ChatMessages.Add(msg);
            await _db.SaveChangesAsync();

            var userConnectionId = ConnectionMapping.GetConnectionId(sessionId);

            if (userConnectionId != null)
            {
                await _hub.Clients.Client(userConnectionId).SendAsync(
                    "ReceiveMessage", "Admin", message, msg.Timestamp);
            }

            await _hub.Clients.Group("Admins").SendAsync(
                "AdminReceiveMessage", sessionId, "Admin", message, msg.Timestamp);
            AdminSessionTracker.MarkAdminJoined(sessionId);
            return Ok();
        }

        [HttpPost]
        public IActionResult MarkAsRead(string sessionId)
        {
            var unread = _db.ChatMessages
                .Where(m => m.SessionId == sessionId && m.Sender != "Admin" && !m.IsRead)
                .ToList();

            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }

            _db.SaveChanges();
            return Ok();
        }
    }


}
