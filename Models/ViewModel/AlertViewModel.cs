using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Models.ViewModels
{
    public class AlertViewModel
    {
        public List<Alert> RecentAlerts { get; set; } = new List<Alert>();
        public List<Alert> AllAlerts { get; set; } = new List<Alert>();
        public List<DateGroupedAlerts> GroupedAlerts { get; set; } = new List<DateGroupedAlerts>();
        public int TotalAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
        public int ActiveAlerts { get; set; }
    }

    public class DateGroupedAlerts
    {
        public DateTime Date { get; set; }
        public List<Alert> Alerts { get; set; } = new List<Alert>();
    }
}