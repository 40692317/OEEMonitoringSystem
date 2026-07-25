using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Models.ViewModels
{
    public class WeeklyReportViewModel
    {
        [Required]
        [Range(1, 12, ErrorMessage = "Please select a valid month.")]
        public int Month { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Please select a valid week number.")]
        public int WeekNumber { get; set; } // 1–5 (max 5 weeks in a month)

        // For dropdown display
        public List<Machine> Machines { get; set; } = new();

        // For multi-selection
        public List<int> SelectedMachineIds { get; set; } = new();

        // Results from report query
        public List<WeeklyOEEReportViewModel> Results { get; set; } = new();
    }
}
