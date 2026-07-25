using Microsoft.ML.Data;

namespace OEEMonitoringSystem.Models.ML
{
    /// <summary>
    /// Input feature vector for OEE regression (predict OEE from current-period metrics).
    /// </summary>
    public class OeeRegressionInput
    {
        public float Availability { get; set; }
        public float Performance { get; set; }
        public float Quality { get; set; }
        public float StoppageCount { get; set; }
        public float StoppageDurationTotalSeconds { get; set; }
        public float ProductionQuantity { get; set; }
        public float DefectQuantity { get; set; }
        public float TargetCycleTime { get; set; }
    }

    /// <summary>
    /// Input + label for training.
    /// </summary>
    public class OeeRegressionRow : OeeRegressionInput
    {
        [ColumnName("Label")]
        public float OEE { get; set; }
    }

    /// <summary>
    /// Prediction output from the regression model.
    /// </summary>
    public class OeeRegressionPrediction
    {
        [ColumnName("Score")]
        public float PredictedOEE { get; set; }
    }
}
