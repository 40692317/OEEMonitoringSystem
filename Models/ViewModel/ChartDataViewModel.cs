namespace OEEMonitoringSystem.Models.ViewModels
{
    public class ChartDataViewModel
    {
        public List<string> Labels { get; set; } = new();

        public List<double> OEEData { get; set; } = new();
        public List<double> AvailabilityData { get; set; } = new();
        public List<double> PerformanceData { get; set; } = new();
        public List<double> QualityData { get; set; } = new();

        public List<double> ThroughputData { get; set; } = new();
        public List<string> ThroughputLabels { get; set; } = new();

        public List<double> OEETrendData { get; set; } = new();
        public List<string> OEETrendLabels { get; set; } = new();

        public List<double> DowntimeData { get; set; } = new();
        public List<string> DowntimeLabels { get; set; } = new();

        public List<double> DefectData { get; set; } = new();
    }
}
