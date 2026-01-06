namespace AIChatBot.Web.Services
{
    using Microsoft.ML;
    using Microsoft.ML.Data;

    public class ChatSentimentService
    {
        private readonly MLContext _mlContext = new(seed: 0);
        private readonly PredictionEngine<SentimentInput, SentimentPrediction> _engine;

        public ChatSentimentService()
        {
                // 🔹 Rich hardcoded training data (expanded for broader coverage)
                var data = new[]
                {
                    // Positive
                    new SentimentInput { Text = "I love this application", Label = "Positive" },
                    new SentimentInput { Text = "Very helpful support", Label = "Positive" },
                    new SentimentInput { Text = "Excellent experience", Label = "Positive" },
                    new SentimentInput { Text = "This saved me so much time!", Label = "Positive" },
                    new SentimentInput { Text = "Fantastic UI and performance", Label = "Positive" },
                    new SentimentInput { Text = "Absolutely brilliant — exceeded expectations", Label = "Positive" },
                    new SentimentInput { Text = "Five stars", Label = "Positive" },
                    new SentimentInput { Text = "I highly recommend this to everyone", Label = "Positive" },
                    new SentimentInput { Text = "Works like a charm", Label = "Positive" },
                    new SentimentInput { Text = "Quick, smooth and reliable", Label = "Positive" },
                    new SentimentInput { Text = "Support team resolved my issue fast", Label = "Positive" },
                    new SentimentInput { Text = "Very impressed with the new features", Label = "Positive" },

                    // Neutral
                    new SentimentInput { Text = "It is okay", Label = "Neutral" },
                    new SentimentInput { Text = "Average service", Label = "Neutral" },
                    new SentimentInput { Text = "Works as expected", Label = "Neutral" },
                    new SentimentInput { Text = "Not bad, could be improved", Label = "Neutral" },
                    new SentimentInput { Text = "I used it yesterday", Label = "Neutral" },
                    new SentimentInput { Text = "No strong feelings either way", Label = "Neutral" },
                    new SentimentInput { Text = "The feature does what it says", Label = "Neutral" },
                    new SentimentInput { Text = "Response time was acceptable", Label = "Neutral" },
                    new SentimentInput { Text = "It started and ran without errors", Label = "Neutral" },
                    new SentimentInput { Text = "Documentation is present", Label = "Neutral" },
                    new SentimentInput { Text = "I have some questions about usage", Label = "Neutral" },
                    new SentimentInput { Text = "It's functional but basic", Label = "Neutral" },

                    // Negative
                    new SentimentInput { Text = "I hate this app", Label = "Negative" },
                    new SentimentInput { Text = "Terrible experience", Label = "Negative" },
                    new SentimentInput { Text = "Very frustrating", Label = "Negative" },
                     new SentimentInput { Text = "Very frustrated", Label = "Negative" },
                    
                    new SentimentInput { Text = "Crashes every time I open it", Label = "Negative" },
                    new SentimentInput { Text = "Support never responded", Label = "Negative" },
                    new SentimentInput { Text = "Features are missing and confusing", Label = "Negative" },
                    new SentimentInput { Text = "Waste of time", Label = "Negative" },
                    new SentimentInput { Text = "Would not recommend", Label = "Negative" },
                    new SentimentInput { Text = "Terrible UX, hard to use", Label = "Negative" },
                    new SentimentInput { Text = "Bugs everywhere :(", Label = "Negative" },
                    new SentimentInput { Text = "Slow and unreliable", Label = "Negative" },
                    new SentimentInput { Text = "It's broken after the last update", Label = "Negative" }
                };

            var trainView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline =
                _mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText("Features", "Text"))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(trainView);
            _engine = _mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentPrediction>(model);
        }

        public string Analyze(string message)
        {
            var result = _engine.Predict(new SentimentInput { Text = message });
            return $"Sentiment detected: <b>{result.PredictedLabel}</b>";
        }
    }

    public class SentimentInput
    {
        public string Text { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class SentimentPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = "";
    }
}
