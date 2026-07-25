using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IAlertService
    {
        Task<List<Alert>> GetActiveAlertsAsync();
        Task<bool> CheckAndTriggerAlertsAsync(int machineId);
        Task<List<Alert>> GetAlertHistoryAsync(int machineId, DateTime startDate, DateTime endDate);
        Task<Alert> CreateAlertAsync(int machineId, string alertType, string message, decimal threshold, decimal currentValue);
    }
}