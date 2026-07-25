using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IOEECalculationService
    {
        Task<OEECalculation> CalculateOEEAsync(int machineId, DateTime periodStart, DateTime periodEnd);
        Task<OEECalculation> CalculateOEEAsync(Machine machine, List<MachineEvent> events, DateTime periodStart, DateTime periodEnd);
        Task<List<OEECalculation>> CalculateOEEForAllMachinesAsync(DateTime periodStart, DateTime periodEnd);
        Task<OEECalculation?> GetLatestOEECalculationAsync(int machineId);
        Task<Dictionary<int, OEECalculation>> GetLatestOEECalculationsAsync(List<int> machineIds);
        Task<OEECalculation> CalculateCurrentOEEAsync(int machineId);
        Task<OEECalculation> CalculateShiftOEEAsync(int machineId, DateTime shiftStart, DateTime shiftEnd);
    }
}