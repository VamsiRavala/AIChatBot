using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIChatBot.Web.Models
{
    /// <summary>
    /// Lightweight schema that the generative model can emit when it wants the bot to perform an action.
    /// Example JSON:
    /// {
    ///   "action": "CreateHRTicket",
    ///   "sessionId": "abc-123",
    ///   "parameters": {
    ///     "title": "Payroll not processed",
    ///     "description": "My salary didn't arrive this month",
    ///     "priority": "High",
    ///     "reporterEmail": "user@contoso.com"
    ///   }
    /// }
    /// </summary>
    public sealed class AgentActionRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }

        // Free-form parameters object (we keep values as JsonElement to be flexible)
        [JsonPropertyName("parameters")]
        public Dictionary<string, JsonElement>? Parameters { get; set; }
    }

    public sealed class AgentActionResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public object? Data { get; init; }
    }
}
