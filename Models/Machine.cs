using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Models
{
    public class Machine
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [Range(0.1, double.MaxValue, ErrorMessage = "Target cycle time must be greater than 0")]
        public double TargetCycleTime { get; set; }

        public bool IsActive { get; set; } = true;

        [BindNever]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [BindNever]
        public string? CurrentState { get; set; }

        public ICollection<MachineEvent> Events { get; set; } = new List<MachineEvent>();
        public ICollection<UserMachine> UserMachines { get; set; } = new List<UserMachine>();
        public ICollection<OEECalculation> OEECalculations { get; set; } = new List<OEECalculation>();
    }
}
