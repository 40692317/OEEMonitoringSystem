using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModels;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IReportService
    {
        Task<List<OEECalculation>> GetOEECalculationsForReportAsync(DateTime startDate, DateTime endDate, List<int>? machineIds = null);
        Task<List<OEECalculation>> GetOEECalculationsAsync(DateTime startDate, DateTime endDate);

        Task<byte[]> ExportToExcelAsync(List<OEECalculation> calculations);
        Task<byte[]> ExportToCsvAsync(List<OEECalculation> calculations);
        Task<List<OEECalculation>> GetHistoricalTrendsAsync(int machineId, DateTime startDate, DateTime endDate);
        Task<List<WeeklyOEEReportViewModel>> GetWeeklyOEEReportAsync(DateTime startUtc, DateTime endUtc, List<int> selectedMachineIds);
        Task<byte[]> ExportWeeklyToCsvAsync(List<WeeklyOEEReportViewModel> weeklyData);
        Task<byte[]> ExportWeeklyToExcelAsync(List<WeeklyOEEReportViewModel> weeklyData);



        // NEW: Weekly OEE Report Methods
        //Task<List<WeeklyOEEReportViewModel>> GetWeeklyOEEReportAsync(DateTime startDate, DateTime endDate);
        //Task<byte[]> ExportWeeklyOEEReportToCsvAsync(DateTime startDate, DateTime endDate);
        //Task<byte[]> ExportWeeklyOEEReportToExcelAsync(DateTime startDate, DateTime endDate);
    }
}