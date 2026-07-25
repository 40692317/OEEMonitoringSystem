using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IMachineService
    {
        Task<List<Machine>> GetAllMachinesAsync();
        Task<List<Machine>> GetMachinesForUserAsync(string userId);
        Task<Machine?> GetMachineByIdAsync(int id);
        Task<Machine> CreateMachineAsync(Machine machine);
        Task<Machine> UpdateMachineAsync(Machine machine);
        Task<bool> DeleteMachineAsync(int id);
        Task AssignMachineToUserAsync(int machineId, string userId);
        Task<List<MachineEvent>> GetMachineEventsAsync(int machineId, DateTime startDate, DateTime endDate);
        Task RemoveMachineFromUserAsync(string userId, int machineId);
        Task<bool> IsUserAuthorizedForMachineAsync(string userId, int machineId);
        // Add these methods to existing interface
        Task<List<ApplicationUser>> GetUsersAssignedToMachineAsync(int machineId);
        Task<List<ApplicationUser>> GetAllUsersAsync();
        Task<bool> MachineExistsByNameAsync(string name);
    }
}