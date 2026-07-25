using OEEMonitoringSystem.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace OEEMonitoringSystem.Models
{
    public class MachineEvent
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public EventType EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Duration { get; set; }
        public int? Quantity { get; set; }

        public Machine Machine { get; set; } = null!;

        [NotMapped]
        public DateTime DisplayTimestamp =>
            Timestamp.Kind == DateTimeKind.Utc ? Timestamp : DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc);
    }
}
