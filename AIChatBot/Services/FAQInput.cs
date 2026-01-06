using Microsoft.ML.Data;

namespace AIChatBot.Services
{
    public class FAQInput
    {
        public string Question { get; set; }
    }
    public class FAQPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedAnswer { get; set; }
    }
}