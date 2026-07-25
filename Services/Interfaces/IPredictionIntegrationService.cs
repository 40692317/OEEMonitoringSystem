using OEEMonitoringSystem.Models.Enums;

namespace OEEMonitoringSystem.Services.Interfaces
{
    /// <summary>
    /// Runs regression and classification inference from OEE/feature data and returns state + optional predicted OEE (Phase 4).
    /// </summary>
    public class PredictionResult
    {
        public MachineState State { get; set; }
        public float? PredictedOEE { get; set; }
    }

    public interface IPredictionIntegrationService
    {
        /// <summary>
        /// Get machine state (rules) and predicted OEE (regression model) from a feature row. Returns state and null PredictedOEE if no model or no row.
        /// </summary>
        Task<PredictionResult> GetPredictionAsync(int machineId, DateTime periodStartUtc, DateTime periodEndUtc);
    }
}
