using AIChatBot.Data;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace AIChatBot.Models
{
    public static class FAQModelTrainer
    {
        public class FAQInput
        {
            public string Question { get; set; }
            public string Label { get; set; }
        }

        public class FAQPrediction
        {
            [ColumnName("PredictedLabel")]
            public string PredictedLabel { get; set; }
        }

        public static void TrainModel(List<HRFAQ> faqs)
        {
            var mlContext = new MLContext();

            // Convert to training data
            var trainingData = faqs
                .Where(f => !string.IsNullOrWhiteSpace(f.Question))
                .Select(f => new FAQInput
                {
                    Question = f.Question,
                    Label = f.Id.ToString() // Predict by ID
                }).ToList();

            var data = mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label", "Label")
                .Append(mlContext.Transforms.Text.FeaturizeText("Features", "Question"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(data);

            mlContext.Model.Save(model, data.Schema, "MLModel.zip");
        }

        // New: compute embeddings for each FAQ question and save them to the DB
        public static void SaveEmbeddingsToDatabase(List<HRFAQ> faqs, AppDbContext db)
        {
            if (faqs == null || faqs.Count == 0) return;

            var mlContext = new MLContext(seed: 0);

            // Load the list of Id/Question pairs into an IDataView
            var input = faqs.Select(f => new { f.Id, f.Question }).ToList();
            var data = mlContext.Data.LoadFromEnumerable(input);

            // Produce a numeric vector named "Features" from text. This is ML.NET's text featurizer (not transformer embeddings).
            var pipeline = mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                inputColumnName: "Question");

            var transformer = pipeline.Fit(data);
            var transformed = transformer.Transform(data);

            // Map transformed rows into a simple result type (Id + Features)
            var embeddings = mlContext.Data.CreateEnumerable<EmbeddingResult>(transformed, reuseRowObject: false).ToList();

            // Persist embeddings back into the HRFAQ objects and save to DB
            foreach (var e in embeddings)
            {
                var faq = faqs.FirstOrDefault(f => f.Id == e.Id);
                if (faq != null)
                {
                    faq.Embedding = e.Features;
                    db.Update(faq);
                }
            }

            db.SaveChanges();
        }

        private class EmbeddingResult
        {
            public int Id { get; set; }
            public float[] Features { get; set; }
        }
    }
}
