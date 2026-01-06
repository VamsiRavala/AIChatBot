using AIChatBot.Services;
using AIChatBot.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIChatBot.Web.Models;

namespace AIChatBot.Web.Services
{
    public class ChatHybridService
    {
        private readonly ChatSearchService _search; 
        private readonly ChatRecommendationService _recommendation;
        private readonly ChatGenAIService _genAi;
        private readonly AgentOrchestratorService _orchestrator;

        public ChatHybridService(
            ChatSearchService search,
            ChatRecommendationService recommendation,
            ChatGenAIService genAi,
            AgentOrchestratorService orchestrator)
        {
            _search = search;
            _recommendation = recommendation;
            _genAi = genAi;
            _orchestrator = orchestrator;
        }

        public async Task<string> GetAnswerAsync(
            string question,
            string? category,
            string sessionId,
            bool useGenAi = false)
        {
            var (answer, faq) = await _search.GetBestAnswerAsync(question, category);

            // Gather top candidates for RAG context
            var top = await _search.GetTopCandidatesAsync(question, category, 5);

            var context = top.Select(f => (f.Id, f.Question, f.Answer));
            string synthesized = string.Empty;

            if (useGenAi)
            {
                synthesized = await _genAi.GenerateAnswerAsync(question, context);

                // Try detect an action request embedded in the generated text (model should output a JSON action block)
                var actionRequest = TryExtractActionRequest(synthesized);
                if (actionRequest != null)
                {
                    // Ensure sessionId is set on the action request when possible
                    if (string.IsNullOrWhiteSpace(actionRequest.SessionId))
                        actionRequest.SessionId = sessionId;

                    var result = await _orchestrator.DispatchAsync(actionRequest);

                    // Append a short confirmation to the user-visible output
                    synthesized += $"<hr/><b>Action result:</b> {(result.Success ? "Success" : "Failed")}: {System.Net.WebUtility.HtmlEncode(result.Message)}";
                }
            }

            // Add related recommendations when we found a FAQ match
            if (faq != null)
            {
                var recommendations = _recommendation.Recommend(question);

                // If we have no generated text, fall back to the quick search answer
                var display = string.IsNullOrWhiteSpace(synthesized) ? (answer ?? string.Empty) : synthesized;
                return $"{display}<hr/><b>You may also be interested in:</b><br/>{recommendations}";
            }

            // If no HQ faq, return synthesized with fallback quick search answer appended
            if (string.IsNullOrWhiteSpace(synthesized))
            {
                // If no synthesized text, return the quick search answer (may be empty)
                return answer ?? string.Empty;
            }

            var recs = _recommendation.Recommend(question);
            return $"{synthesized}<hr/><b>Quick search result:</b><br/>{answer}<hr/><b>Related:</b><br/>{recs}";
        }

        // Looks for a JSON action object in the generated text and returns a parsed AgentActionRequest
        private AgentActionRequest? TryExtractActionRequest(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Heuristic: model should include an object with "action" property. Try to find first JSON object substring.
            // This is intentionally permissive. Production: require strict JSON fences in prompt (e.g. ```json ... ```).
            var firstBrace = text.IndexOf('{');
            if (firstBrace < 0) return null;

            // Try to locate a matching closing brace by scanning (simple brace depth)
            int depth = 0;
            for (int i = firstBrace; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var jsonCandidate = text.Substring(firstBrace, i - firstBrace + 1);
                        try
                        {
                            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var req = JsonSerializer.Deserialize<AgentActionRequest>(jsonCandidate, opts);
                            if (req != null && !string.IsNullOrWhiteSpace(req.Action))
                                return req;
                        }
                        catch
                        {
                            // ignore parse errors and continue searching after this brace block
                        }

                        // If parse failed, continue searching for next '{'
                        var nextStart = text.IndexOf('{', firstBrace + 1);
                        if (nextStart <= firstBrace) break;
                        firstBrace = nextStart;
                        i = firstBrace - 1;
                        depth = 0;
                    }
                }
            }

            return null;
        }
    }
}
