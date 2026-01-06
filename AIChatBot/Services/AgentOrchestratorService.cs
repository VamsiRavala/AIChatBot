using AIChatBot.Data;
using AIChatBot.SignalR;
using AIChatBot.Web.Models;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIChatBot.Services
{
    /// <summary>
    /// Orchestrates authorized agent actions (small, server-side "tool" implementations).
    /// Current supported actions:
    ///  - CreateHRTicket
    /// </summary>
    public class AgentOrchestratorService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public AgentOrchestratorService(AppDbContext db, IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        public async Task<AgentActionResult> DispatchAsync(AgentActionRequest request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Action))
                return new AgentActionResult { Success = false, Message = "Invalid action request." };

            // Normalize action name
            var action = request.Action.Trim();

            try
            {
                if (action.Equals("CreateHRTicket", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("CreateHRticket", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("create_hr_ticket", StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleCreateHRTicketAsync(request, ct);
                }

                return new AgentActionResult { Success = false, Message = $"Unknown action: {action}" };
            }
            catch (Exception ex)
            {
                // Keep error details minimal in client-facing message
                return new AgentActionResult { Success = false, Message = $"Action failed: {ex.Message}" };
            }
        }

        private async Task<AgentActionResult> HandleCreateHRTicketAsync(AgentActionRequest request, CancellationToken ct)
        {
            // Basic authorization: require sessionId or reporterEmail (sessionId must correspond to a registered visitor)
            var sessionId = request.SessionId;
            string? reporterEmail = null;
            string title = string.Empty;
            string description = string.Empty;
            string priority = "Normal";

            if (request.Parameters != null)
            {
                if (request.Parameters.TryGetValue("reporterEmail", out var re) && re.ValueKind == JsonValueKind.String)
                    reporterEmail = re.GetString();

                if (request.Parameters.TryGetValue("title", out var t) && t.ValueKind == JsonValueKind.String)
                    title = t.GetString() ?? string.Empty;

                if (request.Parameters.TryGetValue("description", out var d) && d.ValueKind == JsonValueKind.String)
                    description = d.GetString() ?? string.Empty;

                if (request.Parameters.TryGetValue("priority", out var p) && p.ValueKind == JsonValueKind.String)
                    priority = p.GetString() ?? "Normal";
            }

            // Validate minimally
            if (string.IsNullOrWhiteSpace(title))
                return new AgentActionResult { Success = false, Message = "Missing required parameter: title" };

            // If sessionId provided, ensure there's a visitor record (simple authorization)
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var visitorExists = await _db.Set<Data.Models.ChatVisitor>().AnyAsync(v => v.SessionId == sessionId, ct);
                if (!visitorExists)
                {
                    return new AgentActionResult { Success = false, Message = "Session not registered; cannot create ticket on behalf of an unknown session." };
                }
            }

            // Fallback reporter email from session if missing
            if (string.IsNullOrWhiteSpace(reporterEmail) && !string.IsNullOrWhiteSpace(sessionId))
            {
                var visitor = await _db.Set<Data.Models.ChatVisitor>().FirstOrDefaultAsync(v => v.SessionId == sessionId, ct);
                if (visitor != null)
                    reporterEmail = visitor.Email;
            }

            var ticket = new HRTicket
            {
                Title = title,
                Description = description,
                Priority = priority,
                ReporterEmail = reporterEmail ?? "unknown@internal",
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                Status = "Open"
            };

            _db.Set<HRTicket>().Add(ticket);
            await _db.SaveChangesAsync(ct);

            // Notify admins via SignalR (clients can show a ticket-creation toast)
            await _hubContext.Clients.Group("Admins").SendAsync("HRActionCreated", new { ticket.Id, ticket.Title, ticket.Priority, ticket.ReporterEmail, ticket.CreatedAt }, ct);

            // Optionally notify the user client if connection mapping exists
            //var connId = ChatModels.ConnectionMapping.GetConnectionId(sessionId ?? string.Empty);
            //if (!string.IsNullOrEmpty(connId))
            //{
            //    await _hubContext.Clients.Client(connId).SendAsync("ReceiveMessage", "Bot", $"A support ticket was created: #{ticket.Id} — {ticket.Title}", ct);
            //}

            return new AgentActionResult
            {
                Success = true,
                Message = $"Ticket #{ticket.Id} created successfully.",
                Data = new { ticket.Id, ticket.Title, ticket.Status }
            };
        }
    }
}