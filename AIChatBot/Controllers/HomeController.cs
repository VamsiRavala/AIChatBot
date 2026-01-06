using AIChatBot.Data;
using AIChatBot.Data.Models;
using AIChatBot.Models;
using AIChatBot.Services;
using AIChatBot.SignalR;
using AIChatBot.Web.Models;
using AIChatBot.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace AIChatBot.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ChatSearchService _searchService;

        // Fixed: add service fields that were referenced but not defined
        private readonly ChatSentimentService _sentiment;
        private readonly ChatForecastService _forecast;
        private readonly ChatRecommendationService _recommendation;
        private readonly ChatHybridService _hybrid;

        public HomeController(
            IHttpClientFactory clientFactory,
            IHttpContextAccessor httpContextAccessor,
            AppDbContext dbContext,
            IHubContext<ChatHub> hubContext,
            ChatSearchService searchService,
            ChatSentimentService sentimentService,
            ChatForecastService forecastService,
            ChatRecommendationService recommendationService,
            ChatHybridService hybridService)
        {
            _clientFactory = clientFactory;
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _searchService = searchService;

            // assign newly-injected services
            _sentiment = sentimentService;
            _forecast = forecastService;
            _recommendation = recommendationService;
            _hybrid = hybridService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var messages = _httpContextAccessor.HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory") ?? new();
            var model = new ChatViewModel { Messages = messages };
            return View(model);
        }

        // Added optional 'mode' parameter. If mode is provided and not "Auto", it overrides the IntentDetector.
        [HttpPost]
        public async Task<IActionResult> Index(string userInput, string? category = null, string sessionId = "", string? mode = null)
        {
            var messages = _httpContextAccessor.HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory") ?? new();
            var userMessage = new ChatMessage { Sender = "You", Message = userInput, Timestamp = DateTime.UtcNow };
            messages.Add(userMessage);
            var connectionId = ConnectionMapping.GetConnectionId(sessionId);

            if (connectionId != null)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", "You", userInput);
                await _hubContext.Clients.Client(connectionId).SendAsync("ShowTyping");
                await _hubContext.Clients.Group("Admins").SendAsync("NewUserMessage", sessionId, "User", userInput, DateTime.UtcNow);
            }
                
            if (AdminSessionTracker.IsAdminJoined(sessionId))
            {
                _httpContextAccessor.HttpContext.Session.SetObject("ChatHistory", messages);
                return Ok(); // Exit without bot
            }

            string response;

            // If a mode was set explicitly by the UI and is not "Auto", respect it.
            ChatIntent intent;
            if (!string.IsNullOrWhiteSpace(mode) && !mode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                // Try parse the provided mode into the ChatIntent enum. Fallback to Question (handled as hybrid) if parse fails.
                if (!Enum.TryParse<ChatIntent>(mode, true, out intent))
                {
                    intent = ChatIntent.Question; // treated as default hybrid
                }
            }
            else
            {
                // Fall back to automated detection when mode is Auto or not provided
                intent = IntentDetector.Detect(userInput);
            }

            switch (intent)
            {
                case ChatIntent.Sentiment:
                    response = _sentiment.Analyze(userInput);
                    break;

                case ChatIntent.Forecast:
                    response = _forecast.Forecast();
                    break;

                case ChatIntent.Recommendation:
                    response = _recommendation.Recommend(userInput);
                    break;

                default:
                    response = await _hybrid.GetAnswerAsync(
                        userInput, category, sessionId);
                    break;
            }

            if (connectionId != null)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", "Bot", response);
                await _hubContext.Clients.Group("Admins").SendAsync("NewUserMessage", sessionId, "Bot", response, DateTime.UtcNow);
            }   

            messages.Add(new ChatMessage { Sender = "Bot", Message = response, Timestamp = DateTime.UtcNow });
            _httpContextAccessor.HttpContext.Session.SetObject("ChatHistory", messages);

            //await LogChatAndInteraction(userInput, bestAnswer, matchedFaq, sessionId);

            return Ok();
        }

        // New: Upload file endpoint used by the chat UI
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file, string sessionId, string? mode = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var services = _httpContextAccessor.HttpContext.RequestServices;
            var env = services.GetService<IWebHostEnvironment>();
            if (env == null)
                return StatusCode(500, "Hosting environment not available.");

            var uploadsDir = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var savedName = $"{Guid.NewGuid()}{ext}";
            var savedPath = Path.Combine(uploadsDir, savedName);

            await using (var fs = System.IO.File.Create(savedPath))
            {
                await file.CopyToAsync(fs);
            }

            var publicUrl = $"/uploads/{savedName}";

            // Persist a user message representing the upload
            var messages = _httpContextAccessor.HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory") ?? new();
            messages.Add(new ChatMessage
            {
                Sender = "You",
                Message = $"[file:{file.FileName}] {publicUrl}",
                Timestamp = DateTime.UtcNow
            });

            var connectionId = ConnectionMapping.GetConnectionId(sessionId);
            if (connectionId != null)
            {
                // Notify user client about the uploaded file (user bubble)
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", "You", $"Sent a file: <a href='{publicUrl}' target='_blank'>{file.FileName}</a>");
                await _hubContext.Clients.Client(connectionId).SendAsync("ShowTyping");
                await _hubContext.Clients.Group("Admins").SendAsync("NewUserMessage", sessionId, "User", $"Uploaded file: {file.FileName}", DateTime.UtcNow);
            }

            if (AdminSessionTracker.IsAdminJoined(sessionId))
            {
                _httpContextAccessor.HttpContext.Session.SetObject("ChatHistory", messages);
                return Ok(new { url = publicUrl });
            }

            // Minimal processing: pass a short description to hybrid service so bot can respond.
            string botResponse = await _hybrid.GetAnswerAsync($"User uploaded a file named {file.FileName}. Please review.", null, sessionId);

            if (connectionId != null)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", "Bot", botResponse);
                await _hubContext.Clients.Group("Admins").SendAsync("NewUserMessage", sessionId, "Bot", botResponse, DateTime.UtcNow);
            }

            messages.Add(new ChatMessage { Sender = "Bot", Message = botResponse, Timestamp = DateTime.UtcNow });
            _httpContextAccessor.HttpContext.Session.SetObject("ChatHistory", messages);

            return Ok(new { url = publicUrl });
        }

        private async Task LogChatAndInteraction(string userInput, string answer, HRFAQ? matchedFaq, string? sessionId)
        {
            ChatSession session = _dbContext.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefault(s => s.UserId == sessionId);
            if (session is null)
            {
                session = new ChatSession
                {
                    UserId = sessionId,
                    StartedAt = DateTime.UtcNow,
                    Messages = new List<ChatMessage>(),
                };
                _dbContext.ChatSessions.Add(session);
                await _dbContext.SaveChangesAsync();
            }

            session.Messages.Add(new ChatMessage
            {
                Sender = "You",
                Message = userInput,
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId
            });
            session.Messages.Add(new ChatMessage
            {
                Sender = "Bot",
                Message = answer,
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId
            });
            if (matchedFaq != null)
            {
                _dbContext.FAQInteractionLogs.Add(new FAQInteractionLog
                {
                    UserQuestion = userInput,
                    MatchedQuestion = matchedFaq?.Question,
                    FAQId = matchedFaq?.Id,
                    AnswerReturned = answer,
                    Timestamp = DateTime.UtcNow,
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFeedback(int rating, string? comment)
        {
            string sentiment = "";
            //var sentiment = string.IsNullOrWhiteSpace(comment)
            //    ? "No Comment"
            //    : await _sentimentClient.AnalyzeSentimentAsync(comment);

            //var feedback = new FoodFeedback
            //{
            //    Rating = rating,
            //    Comment = comment,
            //    Sentiment = sentiment
            //};

            //_dbContext.FoodFeedbacks.Add(feedback);
            //await _dbContext.SaveChangesAsync();

            return Json(new { sentiment });
        }

        [HttpPost]
        public IActionResult RegisterVisitor(string sessionId, string name, string email, string phone, string message)
        {
            var visitor = new ChatVisitor
            {
                SessionId = sessionId,
                Name = name,
                Email = email,
                Phone = phone,
                StartedAt = DateTime.UtcNow
            };
            _dbContext.ChatVisitors.Add(visitor);
            _dbContext.SaveChanges();

            return Ok();
        }

        [HttpPost]
        public IActionResult EndSession(string sessionId)
        {
            var visitor = _dbContext.ChatVisitors.FirstOrDefault(v => v.SessionId == sessionId);
            if (visitor != null && visitor.EndedAt == null)
            {
                visitor.EndedAt = DateTime.UtcNow;
                _dbContext.SaveChanges();
            }
            return Ok(new { success = true });
        }
    }
}
