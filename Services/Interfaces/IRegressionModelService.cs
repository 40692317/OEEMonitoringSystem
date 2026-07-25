using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models.ML;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public class RegressionMetricsResult
    {
        public double RMSE { get; set; }
        public double MAE { get; set; }
        public double RSquared { get; set; }
        public int TrainingRows { get; set; }
        public int TestRows { get; set; }
        public string AlgorithmName { get; set; } = "";
    }

    public interface IRegressionModelService
    {
        /// <summary>Train from OEE_dataset.csv. Primary training path.</summary>
        Task<RegressionMetricsResult> TrainFromCsvAsync(string csvPath, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default);

        /// <summary>Train on feature data from DB (fallback).</summary>
        Task<RegressionMetricsResult> TrainAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default);

        Task<RegressionMetricsResult?> EvaluateAsync(DateTime startUtc, DateTime endUtc, int? machineId = null);

        float? Predict(OeeRegressionInput input);

        /// <summary>Predict OEE using the CSV-trained model (same schema as OEE_dataset.csv).</summary>
        float? PredictFromCsv(OeeCsvRow input);
    }
}
