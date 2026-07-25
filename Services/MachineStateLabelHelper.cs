using OEEMonitoringSystem.Models.Enums;

namespace OEEMonitoringSystem.Services
{
    /// <summary>
    /// Rule-based labels for training and fallback when no ML model is loaded (Phase 3).
    /// Thresholds aligned with typical alert rules: Availability 80%, Performance 85%, Quality 90%.
    /// </summary>
    public static class MachineStateLabelHelper
    {
        public const decimal CriticalThreshold = 50;
        public const decimal WarningAvailability = 80;
        public const decimal WarningPerformance = 85;
        public const decimal WarningQuality = 90;
        public const decimal WarningOEE = 70;

        public static string GetStateLabel(decimal availability, decimal performance, decimal quality, decimal oee)
        {
            if (availability < CriticalThreshold || performance < CriticalThreshold || quality < CriticalThreshold || oee < CriticalThreshold)
                return "Critical";
            if (availability < WarningAvailability || performance < WarningPerformance || quality < WarningQuality || oee < WarningOEE)
                return "Warning";
            return "Normal";
        }

        public static MachineState GetState(decimal availability, decimal performance, decimal quality, decimal oee)
        {
            var label = GetStateLabel(availability, performance, quality, oee);
            return label switch
            {
                "Critical" => MachineState.Critical,
                "Warning" => MachineState.Warning,
                _ => MachineState.Normal
            };
        }
    }
}
