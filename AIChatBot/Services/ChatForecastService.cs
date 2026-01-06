using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace AIChatBot.Web.Services
{
    public class ChatForecastService
    {
        private readonly MLContext _mlContext = new(seed: 0);
        private const int Horizon = 6;

        public string Forecast()
        {
            var history = new[]
            {
                new MonthlyData { Year = 2023, Month = 1, Value = 120 },
                new MonthlyData { Year = 2023, Month = 2, Value = 135 },
                new MonthlyData { Year = 2023, Month = 3, Value = 150 },
                new MonthlyData { Year = 2023, Month = 4, Value = 145 },
                new MonthlyData { Year = 2023, Month = 5, Value = 160 },
                new MonthlyData { Year = 2023, Month = 6, Value = 175 },

                new MonthlyData { Year = 2024, Month = 1, Value = 180 },
                new MonthlyData { Year = 2024, Month = 2, Value = 195 },
                new MonthlyData { Year = 2024, Month = 3, Value = 210 },
                new MonthlyData { Year = 2024, Month = 4, Value = 205 }
            };

            var dataView = _mlContext.Data.LoadFromEnumerable(history);

            var pipeline = _mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(ForecastResult.Forecast),
                inputColumnName: nameof(MonthlyData.Value),
                windowSize: 4,
                seriesLength: history.Length,
                trainSize: history.Length,
                horizon: Horizon);

            var model = pipeline.Fit(dataView);

            // CreateTimeSeriesEngine extension lives in Microsoft.ML.Transforms.TimeSeries
            var engine = model.CreateTimeSeriesEngine<MonthlyData, ForecastResult>(_mlContext);

            var forecast = engine.Predict();

            return "📈 Forecast for next 6 months:<br/>" +
                   string.Join("<br/>", forecast.Forecast.Select(v => v.ToString("0.0")));
        }
    }

    public class MonthlyData
    {
        public float Year { get; set; }
        public float Month { get; set; }
        public float Value { get; set; }
    }

    public class ForecastResult
    {
        // VectorType attribute helps ML.NET map the output vector of fixed horizon length
        [VectorType(6)]
        [ColumnName("Forecast")]
        public float[] Forecast { get; set; } = Array.Empty<float>();
    }
}
