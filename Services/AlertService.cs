using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class AlertService : IAlertService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITimezoneService _timezoneService;
        private readonly ILogger<AlertService> _logger;

        public AlertService(
            IServiceProvider serviceProvider,
            ITimezoneService timezoneService,
            ILogger<AlertService> logger)
        {
            _serviceProvider = serviceProvider;
            _timezoneService = timezoneService;
            _logger = logger;
        }

        public async Task<List<Alert>> GetActiveAlertsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var alerts = await context.Alerts
                    .Where(a => !a.IsResolved)
                    .Include(a => a.Machine)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();

                // Convert timestamps to local time for display
                foreach (var alert in alerts)
                {
                    alert.Timestamp = _timezoneService.ConvertUtcToUserTime(alert.Timestamp);
                    if (alert.ResolvedAt.HasValue)
                    {
                        alert.ResolvedAt = _timezoneService.ConvertUtcToUserTime(alert.ResolvedAt.Value);
                    }
                }

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active alerts");
                throw;
            }
        }

        public async Task<bool> CheckAndTriggerAlertsAsync(int machineId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var oeeCalculationService = scope.ServiceProvider.GetRequiredService<IOEECalculationService>();

                var latestOEE = await oeeCalculationService.GetLatestOEECalculationAsync(machineId);
                if (latestOEE == null) return false;

                bool alertTriggered = false;
                alertTriggered |= await CheckMetricAsync(context, machineId, "Availability", latestOEE.Availability, 80);
                alertTriggered |= await CheckMetricAsync(context, machineId, "Performance", latestOEE.Performance, 85);
                alertTriggered |= await CheckMetricAsync(context, machineId, "Quality", latestOEE.Quality, 90);

                return alertTriggered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alerts for machine {MachineId}", machineId);
                return false;
            }
        }

        private async Task<bool> CheckMetricAsync(ApplicationDbContext context, int machineId, string alertType, decimal currentValue, decimal threshold)
        {
            var existingUnresolved = await context.Alerts
                .FirstOrDefaultAsync(a => a.MachineId == machineId && a.AlertType == alertType && !a.IsResolved);

            if (currentValue < threshold)
            {
                if (existingUnresolved != null)
                {
                    existingUnresolved.CurrentValue = currentValue;
                    existingUnresolved.Message = $"{alertType} below threshold: {currentValue:F2}% < {threshold}%";
                    context.Alerts.Update(existingUnresolved);
                    await context.SaveChangesAsync();
                    return false;
                }

                var alert = new Alert
                {
                    MachineId = machineId,
                    AlertType = alertType,
                    Message = $"{alertType} below threshold: {currentValue:F2}% < {threshold}%",
                    Threshold = threshold,
                    CurrentValue = currentValue,
                    Timestamp = DateTime.UtcNow,
                    IsResolved = false
                };
                context.Alerts.Add(alert);
                await context.SaveChangesAsync();
                _logger.LogInformation("Alert created: {AlertType} for machine {MachineId}", alertType, machineId);
                return true;
            }

            if (existingUnresolved != null)
            {
                existingUnresolved.IsResolved = true;
                existingUnresolved.ResolvedAt = DateTime.UtcNow;
                context.Alerts.Update(existingUnresolved);
                await context.SaveChangesAsync();
                _logger.LogInformation("Alert resolved: {AlertType} for machine {MachineId} — {Metric:F2}% now above {Threshold}%",
                    alertType, machineId, currentValue, threshold);
            }

            return false;
        }

        public async Task<List<Alert>> GetAlertHistoryAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var alerts = await context.Alerts
                    .Where(a => a.MachineId == machineId &&
                               a.Timestamp >= startDate &&
                               a.Timestamp <= endDate)
                    .Include(a => a.Machine)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();

                // Convert timestamps to local time for display
                foreach (var alert in alerts)
                {
                    alert.Timestamp = _timezoneService.ConvertUtcToUserTime(alert.Timestamp);
                    if (alert.ResolvedAt.HasValue)
                    {
                        alert.ResolvedAt = _timezoneService.ConvertUtcToUserTime(alert.ResolvedAt.Value);
                    }
                }

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alert history for machine {MachineId}", machineId);
                throw;
            }
        }

        public async Task<Alert> CreateAlertAsync(int machineId, string alertType, string message, decimal threshold, decimal currentValue)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var alert = new Alert
                {
                    MachineId = machineId,
                    AlertType = alertType,
                    Message = message,
                    Threshold = threshold,
                    CurrentValue = currentValue,
                    Timestamp = DateTime.UtcNow, // Store as UTC
                    IsResolved = false
                };

                context.Alerts.Add(alert);
                await context.SaveChangesAsync();

                _logger.LogInformation("Alert created: {AlertType} for machine {MachineId}", alertType, machineId);
                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert for machine {MachineId}", machineId);
                throw;
            }
        }
    }
}