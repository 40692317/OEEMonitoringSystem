// Models/EditMachineViewModel.cs
using System.ComponentModel.DataAnnotations;
using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Models
{
    public class EditMachineViewModel
    {
        public Machine Machine { get; set; } = new Machine();
        public List<string> SelectedUserIds { get; set; } = new List<string>();
        public IEnumerable<ApplicationUser> AllUsers { get; set; } = new List<ApplicationUser>();
        public IEnumerable<ApplicationUser> AssignedUsers { get; set; } = new List<ApplicationUser>();
    }
}