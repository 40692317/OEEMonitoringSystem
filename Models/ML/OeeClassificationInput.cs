using Microsoft.ML.Data;

namespace OEEMonitoringSystem.Models.ML
{
    /// <summary>
    /// Input features for machine state classification (same as regression).
    /// </summary>
    public class OeeClassificationInput
    {
        public float Availability { get; set; }
        public float Performance { get; set; }
        public float Quality { get; set; }
        public float StoppageCount { get; set; }
        public float StoppageDurationTotalSeconds { get; set; }
        public float ProductionQuantity { get; set; }
        public float DefectQuantity { get; set; }
        public float TargetCycleTime { get; set; }

        /// <summary>Label column for the classification pipeline. Use "Normal" as default when predicting.</summary>
        public string Label { get; set; } = "Normal";
    }

    /// <summary>
    /// Input + label for classification training. Label: "Normal", "Warning", "Critical".
    /// </summary>
    public class OeeClassificationRow : OeeClassificationInput
    {
        [ColumnName("Label")]
        public string State { get; set; } = "Normal";
    }

    /// <summary>
    /// Prediction output from the classification model.
    /// </summary>
    public class OeeClassificationPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedState { get; set; } = "Normal";
    }
}
