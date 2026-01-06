using AIChatBot.Data;
using AIChatBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text.RegularExpressions;

namespace AIChatBot.Services
{
    public class ChatSearchService
    {
        private readonly AppDbContext _dbContext;
        private readonly MLContext _mlContext = new(seed: 0);
        private const int CandidateLimit = 50; // limit DB rows returned per request
        private static readonly Regex _wordSplit = new(@"\w+", RegexOptions.Compiled);

        public ChatSearchService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<HRFAQ>> GetTopCandidatesAsync(string userQuestion, string? category, int topN = 5)
        {
            var normalized = (userQuestion ?? string.Empty).Trim().ToLowerInvariant();

            IQueryable<HRFAQ> baseQuery = _dbContext.FAQs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(category))
                baseQuery = baseQuery.Where(f => f.Category == category);

            var candidates = await baseQuery
                .OrderBy(f => f.Id)
                .Take(CandidateLimit)
                .ToListAsync();

            // Basic scoring (reuse same scoring approach as GetBestAnswerAsync but keep many candidates and return topN)
            var docs = candidates.Select(f => new { Faq = f, Hay = (f.Question + " " + f.Answer).ToLowerInvariant() }).ToList();

            var tokens = _wordSplit.Matches(normalized)
                                   .Select(m => m.Value)
                                   .Where(w => w.Length > 2)
                                   .Distinct()
                                   .Take(10)
                                   .ToArray();

            var scored = new List<(HRFAQ Faq, double Score)>();
            foreach (var d in docs)
            {
                double score = 0;
                if (!string.IsNullOrWhiteSpace(normalized) && d.Hay.Contains(normalized))
                    score += 5;
                foreach (var t in tokens)
                {
                    if (d.Hay.Contains(t)) score += 1;
                }
                if (!string.IsNullOrWhiteSpace(category) && string.Equals(d.Faq.Category, category, StringComparison.OrdinalIgnoreCase))
                    score += 0.5;
                scored.Add((d.Faq, score));
            }

            var ordered = scored.OrderByDescending(s => s.Score)
                                .ThenBy(s => s.Faq.Id)
                                .Where(s => s.Score > 0) // drop zero-scored low matches
                                .Select(s => s.Faq)
                                .Take(topN)
                                .ToList();

            // If none scored, return first topN deterministic FAQs (useful to provide context to the generator)
            if (ordered.Count == 0)
                ordered = candidates.Take(topN).ToList();

            return ordered;
        }

        public async Task<(string answer, HRFAQ? matchedFaq)> GetBestAnswerAsync(string userQuestion, string? category)
        {
            var normalized = (userQuestion ?? string.Empty).Trim().ToLowerInvariant();

            // Quick greeting handling
            var greetings = new Dictionary<string, string>
            {
                { "hi", "Hello! How can I assist you today?" },
                { "hello", "Hi there! How can I help?" },
                { "hey", "Hey! What would you like to know?" },
                { "how are you", "I'm just a bot, but I'm doing great! How can I assist you?" },
                { "good morning", "Good morning! What can I help you with?" },
                { "good afternoon", "Good afternoon! Let me know how I can help." },
                { "good evening", "Good evening! I'm here to help with your questions." }
            };

            foreach (var pair in greetings)
            {
                if (normalized.Contains(pair.Key))
                    return (pair.Value, null);
            }

            // Tokenize and pick a few meaningful tokens to guide the DB fetch
            var tokens = _wordSplit.Matches(normalized)
                                   .Select(m => m.Value)
                                   .Where(w => w.Length > 2) // skip short words
                                   .Distinct()
                                   .Take(10)
                                   .ToArray();

            // If no useful tokens, fall back to category-based or top-ranked FAQs
            IQueryable<HRFAQ> baseQuery = _dbContext.FAQs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(category))
                baseQuery = baseQuery.Where(f => f.Category == category);

            // Perform a single limited DB read (keeps DB work bounded)
            var candidates = await baseQuery
                .OrderBy(f => f.Id) // deterministic order; adjust if you have ranking column
                .Take(CandidateLimit)
                .ToListAsync();

            if (candidates.Count == 0)
                return ("Sorry, I couldn't find a matching FAQ.", null);

            // Prepare documents for ML.NET - combine question + answer as the text field
            var docs = candidates.Select(f => new DocumentData
            {
                Id = f.Id,
                Text = (f.Question + " " + f.Answer).ToLowerInvariant()
            }).ToList();

            // Build a simple text featurizer (TF-IDF + ngrams, stopwords, etc.) using ML.NET
            var dataView = _mlContext.Data.LoadFromEnumerable(docs);

            var pipeline = _mlContext.Transforms.Text.FeaturizeText(
                                outputColumnName: "Features",
                                inputColumnName: nameof(DocumentData.Text));

            var transformer = pipeline.Fit(dataView);
            var transformed = transformer.Transform(dataView);

            // Extract feature vectors for candidates
            var transformedDocs = _mlContext.Data.CreateEnumerable<TransformedDoc>(transformed, reuseRowObject: false)
                                                .ToDictionary(td => td.Id, td => td.Features);

            // Transform the user query into the same feature space
            var queryData = new[] { new QueryData { Text = normalized } };
            var queryView = _mlContext.Data.LoadFromEnumerable(queryData);
            var transformedQueryView = transformer.Transform(queryView);
            var queryEnumerable = _mlContext.Data.CreateEnumerable<TransformedQuery>(transformedQueryView, reuseRowObject: false).ToList();
            var queryFeatures = queryEnumerable.FirstOrDefault()?.Features;

            // Score candidates in-memory using token overlap + exact phrase boost + ML similarity (cosine)
            HRFAQ? best = null;
            double bestScore = double.MinValue;

            foreach (var faq in candidates)
            {
                var hay = (faq.Question + " " + faq.Answer).ToLowerInvariant();
                double score = 0;

                // exact substring match gets a high boost
                if (!string.IsNullOrWhiteSpace(normalized) && hay.Contains(normalized))
                    score += 5;

                // token overlap
                foreach (var t in tokens)
                {
                    if (hay.Contains(t))
                        score += 1;
                }

                // small boost for category match when provided
                if (!string.IsNullOrWhiteSpace(category) && string.Equals(faq.Category, category, StringComparison.OrdinalIgnoreCase))
                    score += 0.5;

                // ML-based similarity (cosine) if available
                if (queryFeatures != null && transformedDocs.TryGetValue(faq.Id, out var candFeatures) && candFeatures != null)
                {
                    var mlSim = CosineSimilarity(queryFeatures, candFeatures);
                    // weight ML signal so it's meaningful but doesn't completely dominate short exact matches
                    score += mlSim * 4.0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = faq;
                }
            }

            // A threshold avoids returning irrelevant answers
            if (best == null || bestScore < 1)
                return ("Sorry, I couldn't find a good answer to your question.", null);

            return (best.Answer, best);
        }

        // Helper data types for ML.NET mapping
        private sealed class DocumentData
        {
            public int Id { get; set; }

            public string Text { get; set; } = string.Empty;
        }

        private sealed class TransformedDoc
        {
            [VectorType]
            public float[] Features { get; set; } = Array.Empty<float>();

            public int Id { get; set; }
        }

        private sealed class QueryData
        {
            public string Text { get; set; } = string.Empty;
        }

        private sealed class TransformedQuery
        {
            [VectorType]
            public float[] Features { get; set; } = Array.Empty<float>();
        }

        private static double CosineSimilarity(float[]? a, float[]? b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0)
                return 0.0;

            // If lengths differ, compute dot using the overlapping prefix (FeaturizeText should produce same length)
            var len = Math.Min(a.Length, b.Length);

            double dot = 0;
            double magA = 0;
            double magB = 0;
            for (int i = 0; i < len; i++)
            {
                dot += (double)a[i] * b[i];
                magA += (double)a[i] * a[i];
                magB += (double)b[i] * b[i];
            }

            if (magA <= 0 || magB <= 0)
                return 0.0;

            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }
    }
}
