using System;

namespace OEEMonitoringSystem.Models.ViewModels
{
    public class WeeklyOEEReportViewModel
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;

        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int WeekNumber { get; set; }
        public double Availability { get; set; }
        public double Performance { get; set; }
        public double Quality { get; set; }
        public double OEE { get; set; }
    }
}
