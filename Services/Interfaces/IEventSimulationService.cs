using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IEventSimulationService
    {
        Task SimulateMachineEventAsync(int machineId, EventType eventType);
        Task SimulateRandomEventAsync(int machineId);
        Task SimulateEventsForAllMachinesAsync();
        Task BroadcastOEEUpdateAsync(int machineId, double oee, double availability, double performance, double quality);
        Task<List<Machine>> GetActiveMachinesAsync();
        Task<bool> SimulateMachineEventAsync(
          int machineId,
          EventType eventType,
          DateTime timestamp,
          int? quantity = null,
          double? duration = null);
    }
}

