using OEEMonitoringSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Models.ViewModels
{
    public class ReportViewModel
    {

        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);

       
        public DateTime EndDate { get; set; } = DateTime.Today;

        public List<OEECalculation> OEECalculations { get; set; } = new List<OEECalculation>();

        public List<Machine> Machines { get; set; } = new List<Machine>();

        // ✅ Selected machine IDs
        public List<int> SelectedMachineIds { get; set; } = new List<int>();
        public List<WeeklyOEEReportViewModel> WeeklyReports { get; set; } = new();
    }
}
