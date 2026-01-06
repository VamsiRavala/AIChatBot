namespace AIChatBot.Web.Services
{
    using System.Text.RegularExpressions;

        public class ChatRecommendationService
        {
            private readonly Dictionary<uint, string> _items = new()
            {
                {1, "Leave Policy"},
                {2, "Payroll Process"},
                {3, "Benefits Overview"},
                {4, "Holiday Calendar"},
                {5, "Insurance Policy"}
            };

            public ChatRecommendationService()
            {
            }

            /// <summary>
            /// Recommend topics based on the user's search input (text similarity).
            /// If the search input is empty or null, returns the first 3 items.
            /// </summary>
            public string Recommend(string searchInput)
            {
                if (string.IsNullOrWhiteSpace(searchInput))
                {
                    var fallback = _items
                        .Take(3)
                        .Select(x => $"• {x.Value}");
                    return "Recommended topics for you:<br/>" + string.Join("<br/>", fallback);
                }

                var ranked = _items
                    .Select(i => new
                    {
                        Name = i.Value,
                        Score = ComputeSimilarity(searchInput, i.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Name)
                    .Take(3)
                    .Select(x => $"• {x.Name}");

                return "Recommended topics for you:<br/>" + string.Join("<br/>", ranked);
            }

        // Simple cosine similarity on token frequency vectors (case-insensitive, punctuation removed).
        private static double ComputeSimilarity(string a, string b)
        {
            var tokensA = Tokenize(a);
            var tokensB = Tokenize(b);

            if (tokensA.Length == 0 || tokensB.Length == 0)
            {
                return 0d;
            }

            var freqA = tokensA.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            var freqB = tokensB.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

            double dot = 0;
            foreach (var (term, countA) in freqA)
            {
                if (freqB.TryGetValue(term, out var countB))
                {
                    dot += countA * countB;
                }
            }

            double normA = Math.Sqrt(freqA.Values.Sum(v => v * v));
            double normB = Math.Sqrt(freqB.Values.Sum(v => v * v));

            return (normA > 0 && normB > 0) ? dot / (normA * normB) : 0d;
        }

        private static string[] Tokenize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            // Lowercase, remove punctuation and split on non-word characters.
            var cleaned = input.ToLowerInvariant();
            var tokens = Regex.Split(cleaned, @"\W+")
                              .Where(s => !string.IsNullOrWhiteSpace(s))
                              .ToArray();
            return tokens;
        }
    }
}
