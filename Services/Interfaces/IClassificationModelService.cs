using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models.ML;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public class ClassificationMetricsResult
    {
        public double Accuracy { get; set; }
        public double MacroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public int TrainingRows { get; set; }
        public int TestRows { get; set; }
        public string AlgorithmName { get; set; } = "";
        public List<List<double>> ConfusionMatrixCounts { get; set; } = new();
        public List<string> ClassLabels { get; set; } = new();
    }

    public interface IClassificationModelService
    {
        /// <summary>Train from OEE_dataset.csv. Primary training path.</summary>
        Task<ClassificationMetricsResult> TrainFromCsvAsync(string csvPath, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default);

        Task<ClassificationMetricsResult> TrainAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default);
        Task<ClassificationMetricsResult?> EvaluateAsync(DateTime startUtc, DateTime endUtc, int? machineId = null);
        MachineState? Predict(OeeClassificationInput input);

        /// <summary>Predict state (Normal/Warning/Critical) using the CSV-trained model.</summary>
        MachineState? PredictFromCsv(OeeCsvRow input);

        MachineState GetStateFromRules(decimal availability, decimal performance, decimal quality, decimal oee);
    }
}
