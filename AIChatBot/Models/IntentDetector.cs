using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIChatBot.Web.Models
{
    public enum ChatIntent
    {
        Question,
        Sentiment,
        Recommendation,
        Forecast,
        Unknown
    }

    public sealed record IntentResult(ChatIntent Intent, double Confidence, IReadOnlyList<string> MatchedKeywords, string Reason);

    public static class IntentDetector
    {
        // Public compatibility method: preserves original API
        public static ChatIntent Detect(string text)
        {
            return DetectWithResult(text).Intent;
        }

        // New advanced detection API with confidence and matched keywords
        public static IntentResult DetectWithResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new IntentResult(ChatIntent.Unknown, 0.0, Array.Empty<string>(), "Empty or whitespace input");

            var normalized = Normalize(text);

            // Quick strong signals
            if (IsQuestionByPunctuationOrWords(text, normalized, out var qMatches))
            {
                return new IntentResult(ChatIntent.Question, 0.98, qMatches, "Question punctuation or interrogative word found");
            }

            // Keyword dictionaries with weights (higher = stronger signal)
            var intentKeywords = new Dictionary<ChatIntent, Dictionary<string, double>>(capacity: 4)
            {
                [ChatIntent.Forecast] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["forecast"] = 3.0, ["predict"] = 3.0, ["projection"] = 2.0, ["trend"] = 2.0, ["estimate"] = 2.0, ["will"] = 1.0
                },
                [ChatIntent.Recommendation] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["recommend"] = 3.0, ["suggest"] = 3.0, ["best"] = 2.0, ["should"] = 1.5, ["try"] = 1.0, ["option"] = 1.0
                },
                [ChatIntent.Sentiment] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    // positive
                    ["happy"] = 2.0, ["good"] = 1.5, ["great"] = 2.0, ["love"] = 2.0,
                    // negative
                    ["angry"] = 2.0, ["sad"] = 2.0, ["upset"] = 1.5, ["frustrated"] = 2.0,
                    // feelings general
                    ["feel"] = 1.2, ["feeling"] = 1.2
                }
            };

            // Tokenize normalized input
            var tokens = Tokenize(normalized).ToList();

            var matchedKeywords = new List<string>();
            var scores = new Dictionary<ChatIntent, double>();

            foreach (var intent in intentKeywords.Keys)
            {
                double score = 0.0;
                foreach (var kv in intentKeywords[intent])
                {
                    var key = kv.Key;
                    var weight = kv.Value;

                    // Phrase match (multi-word)
                    if (key.Contains(' '))
                    {
                        if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase))
                        {
                            score += weight;
                            matchedKeywords.Add(key);
                        }

                        continue;
                    }

                    // Exact token match
                    if (tokens.Contains(key))
                    {
                        score += weight;
                        matchedKeywords.Add(key);
                        continue;
                    }

                    // Stemmed token match
                    var stem = Stem(key);
                    if (tokens.Contains(stem))
                    {
                        score += weight * 0.9;
                        matchedKeywords.Add(key);
                        continue;
                    }

                    // Fuzzy match (allow small typos)
                    if (tokens.Any(t => AreClose(t, key)))
                    {
                        score += weight * 0.7;
                        matchedKeywords.Add(key + " (fuzzy)");
                    }
                }

                scores[intent] = score;
            }

            // Sentiment special handling: compute polarity (positive vs negative)
            double sentimentPolarity = ComputeSentimentPolarity(tokens);
            if (Math.Abs(sentimentPolarity) >= 0.6)
            {
                // strong sentiment signal
                scores[ChatIntent.Sentiment] = Math.Max(scores.GetValueOrDefault(ChatIntent.Sentiment), Math.Abs(sentimentPolarity) * 3.0);
                if (sentimentPolarity > 0)
                    matchedKeywords.Add("positive_sentiment");
                else
                    matchedKeywords.Add("negative_sentiment");
            }

            // Negation handling: if negation exists near a keyword, reduce the effective score for that keyword
            if (HasNegationNearby(normalized))
            {
                foreach (var k in scores.Keys.ToList())
                    scores[k] *= 0.6; // dampen signals when negation present
            }

            // Choose intent with highest score
            var best = scores.OrderByDescending(kv => kv.Value).FirstOrDefault();
            var bestIntent = best.Key;
            var bestScore = best.Value;

            // If no strong signals, fallback to Question if contains a question word (already handled) or Unknown
            double maxPossibleScore = intentKeywords.Values.SelectMany(d => d.Values).Max();
            double confidence = Math.Min(1.0, bestScore / Math.Max(1.0, maxPossibleScore));

            // If scores are all near zero, fallback to Unknown
            if (bestScore < 0.5)
            {
                // If some keywords matched but weak, still return the one with weak confidence
                if (matchedKeywords.Count == 0)
                    return new IntentResult(ChatIntent.Unknown, Math.Round(confidence, 2), matchedKeywords, "No decisive keywords matched");
            }

            var reason = $"BestScore={bestScore:F2}; Confidence computed from maxPossible={maxPossibleScore:F2}";
            return new IntentResult(bestIntent, Math.Round(confidence, 2), matchedKeywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), reason);
        }

        // --- Helpers ---

        private static string Normalize(string text)
        {
            // Lowercase and normalize whitespace
            var t = text.ToLowerInvariant().Trim();
            // Replace common punctuation with spaces (keeps words separated)
            t = Regex.Replace(t, @"[^\p{L}\p{Nd}]+", " ");
            t = Regex.Replace(t, @"\s{2,}", " ");
            return t;
        }

        private static IEnumerable<string> Tokenize(string normalized)
        {
            return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .Select(Stem);
        }

        private static string Stem(string token)
        {
            // Very small, safe stemmer: strip common English suffixes
            if (token.EndsWith("ing", StringComparison.OrdinalIgnoreCase) && token.Length > 4)
                return token.Substring(0, token.Length - 3);
            if (token.EndsWith("ed", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
                return token.Substring(0, token.Length - 2);
            if (token.EndsWith("es", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
                return token.Substring(0, token.Length - 2);
            if (token.EndsWith("s", StringComparison.OrdinalIgnoreCase) && token.Length > 2)
                return token.Substring(0, token.Length - 1);
            return token;
        }

        private static bool AreClose(string a, string b)
        {
            // Simple fuzzy: true if Levenshtein distance <= 1 for short tokens or <=2 for longer
            var dist = LevenshteinDistance(a, b);
            return dist <= (b.Length <= 4 ? 1 : 2);
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var la = a.Length;
            var lb = b.Length;
            var d = new int[la + 1, lb + 1];

            for (var i = 0; i <= la; i++) d[i, 0] = i;
            for (var j = 0; j <= lb; j++) d[0, j] = j;

            for (var i = 1; i <= la; i++)
            {
                for (var j = 1; j <= lb; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[la, lb];
        }

        private static bool IsQuestionByPunctuationOrWords(string originalText, string normalized, out List<string> matches)
        {
            matches = new List<string>();
            if (originalText.Contains('?'))
            {
                matches.Add("?");
                return true;
            }

            var questionWords = new[] { "who", "what", "when", "where", "why", "how", "which", "is", "are", "do", "does", "did", "can", "could", "would", "will" };
            var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length > 0 && questionWords.Contains(tokens[0]))
            {
                matches.Add(tokens[0]);
                return true;
            }

            // check any interrogative word presence (less strong)
            foreach (var q in questionWords)
            {
                if (tokens.Contains(q))
                {
                    matches.Add(q);
                }
            }

            return matches.Count > 0;
        }

        private static double ComputeSentimentPolarity(IReadOnlyList<string> tokens)
        {
            // Very small lexicon-based polarity. Positive words add +1, negative -1.
            var positives = new[] { "happy", "good", "great", "love", "excellent", "awesome", "positive" };
            var negatives = new[] { "angry", "sad", "bad", "terrible", "hate", "frustrated", "upset", "negative" };

            double score = 0;
            foreach (var t in tokens)
            {
                if (positives.Contains(t)) score += 1.0;
                if (negatives.Contains(t)) score -= 1.0;
            }

            if (score == 0) return 0.0;

            // Normalize to -1..1 range with soft scaling
            return Math.Tanh(score / 2.0);
        }

        private static bool HasNegationNearby(string normalized)
        {
            // Detect common negations which may flip or weaken intent
            var negations = new[] { "not", "no", "never", "none", "can't", "cannot", "dont", "don't" };
            foreach (var n in negations)
            {
                if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(n)}\b", RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
