using Microsoft.ML.Data;

namespace OEEMonitoringSystem.Models.ML
{
    /// <summary>
    /// One row from ML/OEE_dataset.csv — used for CSV-based training.
    /// Columns match those written by prepare_dataset.py.
    /// </summary>
    public class OeeCsvRow
    {
        [LoadColumn(0)] public float Availability { get; set; }
        [LoadColumn(1)] public float Performance  { get; set; }
        [LoadColumn(2)] public float Actual       { get; set; }
        [LoadColumn(3)] public float Packs        { get; set; }
        [LoadColumn(4)] public float Stop         { get; set; }
        [LoadColumn(5)] public float Start        { get; set; }
        [LoadColumn(6)] public float Processing   { get; set; }
        [LoadColumn(7)] public float Printing     { get; set; }
        [LoadColumn(8)] public float Idle         { get; set; }
        [LoadColumn(9)]  public float OEE         { get; set; }   // regression label
        [LoadColumn(10)] public string State      { get; set; } = "Normal"; // classification label
    }

    /// <summary>Output when predicting OEE from the CSV-trained regression model.</summary>
    public class OeeCsvRegressionPrediction
    {
        [ColumnName("Score")]
        public float PredictedOEE { get; set; }
    }

    /// <summary>Output when predicting state from the CSV-trained classification model.</summary>
    public class OeeCsvClassificationPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedState { get; set; } = "Normal";
    }
}
