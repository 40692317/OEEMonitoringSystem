namespace OEEMonitoringSystem.Models
{
    public class OEECalculation
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public DateTime CalculationTime { get; set; }

        public decimal Availability { get; set; }
        public decimal Performance { get; set; }
        public decimal Quality { get; set; }
        public decimal OEE { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public Machine Machine { get; set; } = null!;

        public string DisplayCalculationTime =>
            EnsureUtc(CalculationTime).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        public string DisplayPeriodStart =>
            EnsureUtc(PeriodStart).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        public string DisplayPeriodEnd =>
            EnsureUtc(PeriodEnd).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
