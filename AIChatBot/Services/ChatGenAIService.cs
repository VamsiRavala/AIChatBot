using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIChatBot.Services
{
    public class ChatGenAIService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _apiKey;
        private readonly string _apiBase;
        private readonly string _model;

        public ChatGenAIService(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _apiKey = config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            _apiBase = config["OpenAI:ApiBase"] ?? "https://api.openai.com/v1";
            _model = config["OpenAI:Model"] ?? "gpt-4o-mini";
           // if (string.IsNullOrWhiteSpace(_apiKey))
             //   throw new InvalidOperationException("OpenAI API key not configured. Set OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        public async Task<string> GenerateAnswerAsync(string question, IEnumerable<(int Id, string Question, string Answer)> contextSnippets, CancellationToken ct = default)
        {
            // Build a clear system + user prompt that instructs the model to use the provided contexts and cite them.
            var system = """
You are a helpful HR assistant for a company. Use only the supplied context snippets when directly answering factual company questions; if you must add general guidance, mark it clearly. When you use context snippets, cite them inline like [FAQ#Id]. If the context does not contain an exact answer, provide a concise generated answer and recommend contacting HR. Keep the answer short (<= 400 words).
""";

            // Build a user message that includes the snippets (label them)
            var sb = new StringBuilder();
            sb.AppendLine("Context snippets (use only when relevant):");
            foreach (var s in contextSnippets)
            {
                var safeQ = s.Question?.Replace("\n", " ").Trim();
                var safeA = s.Answer?.Replace("\n", " ").Trim();
                sb.AppendLine($"[FAQ#{s.Id}] Q: {safeQ} A: {safeA}");
            }
            sb.AppendLine();
            sb.AppendLine("User question:");
            sb.AppendLine(question);
            sb.AppendLine();
            sb.AppendLine("Answer (cite any FAQ as [FAQ#Id], or say 'No exact FAQ found' and give a short, honest response).");

            var messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = sb.ToString() }
            };

            var payload = new
            {
                model = _model,
                messages = messages,
                temperature = 0.2,
                max_tokens = 700,
                top_p = 1.0
            };

            var client = _httpFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBase}/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var resp = await client.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"GenAI request failed ({resp.StatusCode}): {body}");
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            // Navigate response: choices[0].message.content
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            // Fallback: attempt to read string
            return await resp.Content.ReadAsStringAsync(ct);
        }
    }
}