using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal Threshold { get; set; }
        public decimal CurrentValue { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsResolved { get; set; } = false;
        public DateTime? ResolvedAt { get; set; }

        public Machine Machine { get; set; } = null!;

        public string DisplayTimestamp =>
            EnsureUtc(Timestamp).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        public string DisplayResolvedAt =>
            ResolvedAt.HasValue
                ? EnsureUtc(ResolvedAt.Value).ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
                : string.Empty;

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
