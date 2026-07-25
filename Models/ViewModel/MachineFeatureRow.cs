namespace OEEMonitoringSystem.Models.ViewModel
{
    /// <summary>
    /// One row of features for ML: one period per machine (from OEECalculation + event aggregates).
    /// </summary>
    public class MachineFeatureRow
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double TargetCycleTime { get; set; }

        public decimal Availability { get; set; }
        public decimal Performance { get; set; }
        public decimal Quality { get; set; }
        public decimal OEE { get; set; }

        public int StoppageCount { get; set; }
        public double StoppageDurationTotalSeconds { get; set; }
        public int ProductionQuantity { get; set; }
        public int DefectQuantity { get; set; }
    }
}
