using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Models.ViewModels
{
    public class MachineViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Remote(action: "IsMachineNameInUse", controller: "Machine")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [Range(0.1, double.MaxValue, ErrorMessage = "Target cycle time must be greater than 0")]
        public double TargetCycleTime { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class MachineListViewModel
    {
        public List<Machine> Machines { get; set; } = new List<Machine>();
    }
}