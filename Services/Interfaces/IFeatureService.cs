using OEEMonitoringSystem.Models.ViewModel;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface IFeatureService
    {
        /// <summary>
        /// Builds feature rows for ML: one per (machine, period) using OEECalculations and event aggregates in the given range.
        /// When <paramref name="topN"/> is set, only the N most-recent calculations are processed (avoids N+1 overhead for real-time prediction).
        /// </summary>
        Task<List<MachineFeatureRow>> GetFeaturesForPeriodAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, int? topN = null);

        Task<byte[]> ExportFeaturesToCsvAsync(List<MachineFeatureRow> rows);
        Task<byte[]> ExportFeaturesToExcelAsync(List<MachineFeatureRow> rows);
    }
}
