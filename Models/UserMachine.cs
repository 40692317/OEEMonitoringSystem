namespace OEEMonitoringSystem.Models
{
    public class UserMachine
    {
        public string UserId { get; set; } = string.Empty;
        public int MachineId { get; set; }

        // Navigation properties
        public Machine Machine { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}