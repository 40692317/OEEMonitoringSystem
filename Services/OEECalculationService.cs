
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Hubs;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OEEMonitoringSystem.Services
{
    /// <summary>
    /// Service for calculating Overall Equipment Effectiveness (OEE) for machines.
    /// OEE = (Availability × Performance × Quality) / 10000, where:
    /// - Availability: (Runtime / Total Time) × 100
    /// - Performance: (IdealCycleTime × Total Production / Runtime) × 100 (capped at 100%)
    /// - Quality: (Good Parts / Total Parts) × 100
    /// 
    /// This service is generic, using each machine's TargetCycleTime, and handles all EventType values:
    /// - MachineStarted: Starts a runtime segment.
    /// - MachineStopped, Maintenance, Idle: Ends a runtime segment (downtime).
    /// - ProductionCycle: Adds to total production count.
    /// - DefectiveUnit: Adds to defective count for quality.
    /// 
    /// Edge cases handled:
    /// - No events: Returns 0 for all metrics.
    /// - Invalid period (start > end): Returns 0 metrics.
    /// - Negative/zero TargetCycleTime: Performance = 0.
    /// - Negative quantities: Clamped to 0.
    /// - Multiple/overlapping starts/stops: Accumulates runtime using timestamps.
    /// - Out-of-order events: Sorted by timestamp.
    /// - No production: Performance/Quality = 0.
    /// - Still running at period end: Includes time from last start.
    /// - Database errors: Logs and throws meaningful exceptions.
    /// - Real-life scenarios: Idle (e.g., material wait), Maintenance (planned downtime), Defective (quality issues).
    /// 
    /// Logging: Detailed for debugging (inputs, intermediates, outputs) but minimal to avoid performance impact.
    /// </summary>
    public class OEECalculationService : IOEECalculationService
    {
        private readonly IServiceProvider _serviceProvider; // Provides DB context via dependency injection
        private readonly ILogger<OEECalculationService> _logger; // Logger for errors, warnings, and debug info
        private readonly IHubContext<OEEHub> _hubContext;
        /// <summary>
        /// Constructor to inject dependencies.
        /// </summary>
        /// <param name="serviceProvider">Service provider for DB context.</param>
        /// <param name="logger">Logger for this service.</param>
        public OEECalculationService(
            IServiceProvider serviceProvider,
            ILogger<OEECalculationService> logger,
            IHubContext<OEEHub> hubContext
            )
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <summary>
        /// Calculates OEE for a machine over a specified period by fetching machine and events from DB.
        /// Validates machine existence and period, then computes OEE.
        /// </summary>
        /// <param name="machineId">Machine ID.</param>
        /// <param name="periodStart">Start of the period (UTC).</param>
        /// <param name="periodEnd">End of the period (UTC).</param>
        /// <returns>OEE calculation result.</returns>
        /// <exception cref="ArgumentException">If machine not found.</exception>
        public async Task<OEECalculation> CalculateOEEAsync(int machineId, DateTime periodStart, DateTime periodEnd)
        {
            try
            {
                // Create scope for DB context (thread-safe for background tasks)
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Validate machine existence
                var machine = await context.Machines.FindAsync(machineId);
                if (machine == null)
                {
                    _logger.LogWarning("Machine with ID {MachineId} not found for OEE calculation", machineId);
                    throw new ArgumentException($"Machine with ID {machineId} not found");
                }

                // Fetch events within the period
                var events = await context.MachineEvents
                    .Where(e => e.MachineId == machineId && e.Timestamp >= periodStart && e.Timestamp <= periodEnd)
                    .ToListAsync();

                return CalculateOEE(machine, events, periodStart, periodEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating OEE for machine {MachineId} from {PeriodStart} to {PeriodEnd}",
                    machineId, periodStart, periodEnd);
                throw;
            }
        }

        /// <summary>
        /// Overload to calculate OEE with provided machine and events (useful for testing or batch processing).
        /// </summary>
        /// <param name="machine">Machine object.</param>
        /// <param name="events">List of events.</param>
        /// <param name="periodStart">Start of the period (UTC).</param>
        /// <param name="periodEnd">End of the period (UTC).</param>
        /// <returns>OEE calculation result.</returns>
        public async Task<OEECalculation> CalculateOEEAsync(Machine machine, List<MachineEvent> events, DateTime periodStart, DateTime periodEnd)
        {
            try
            {
                return CalculateOEE(machine, events, periodStart, periodEnd);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating OEE for machine {MachineId} from {PeriodStart} to {PeriodEnd}",
                    machine.Id, periodStart, periodEnd);
                throw;
            }
        }

        /// <summary>
        /// Core OEE calculation logic. Computes Availability, Performance, Quality, and OEE.
        /// Handles all event types and edge cases:
        /// - Invalid period: Returns 0 metrics.
        /// - No events: Returns 0 metrics.
        /// - Negative/zero TargetCycleTime: Performance = 0.
        /// - Negative quantities: Clamped to 0.
        /// - Multiple starts/stops: Accumulates runtime.
        /// - Still running: Includes time from last start to period end.
        /// - Out-of-order events: Sorts by timestamp.
        /// </summary>
        /// <param name="machine">Machine with TargetCycleTime.</param>
        /// <param name="events">Events in the period.</param>
        /// <param name="periodStart">Start of the period (UTC).</param>
        /// <param name="periodEnd">End of the period (UTC).</param>
        /// <returns>OEE calculation result.</returns>
        private OEECalculation CalculateOEE(Machine machine, List<MachineEvent> events, DateTime periodStart, DateTime periodEnd)
        {
            try
            {
                // Validate period (start > end)
                if (periodStart > periodEnd)
                {
                    _logger.LogWarning("Invalid period for machine {MachineId}: Start {PeriodStart} > End {PeriodEnd}",
                        machine.Id, periodStart, periodEnd);
                    return new OEECalculation
                    {
                        MachineId = machine.Id,
                        CalculationTime = DateTime.UtcNow,
                        Availability = 0,
                        Performance = 0,
                        Quality = 0,
                        OEE = 0,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd
                    };
                }

                // Sort events by timestamp (handles out-of-order events)
                var periodEvents = events
                    .Where(e => e.Timestamp >= periodStart && e.Timestamp <= periodEnd)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                // Log event count (edge case: no events)
                _logger.LogDebug("Machine {MachineId}: Found {EventCount} events in period {PeriodStart} to {PeriodEnd}",
                    machine.Id, periodEvents.Count, periodStart, periodEnd);

                // No events: return zero metrics
                if (periodEvents.Count == 0)
                {
                    _logger.LogWarning("No events found for machine {MachineId} in period {PeriodStart} to {PeriodEnd}",
                        machine.Id, periodStart, periodEnd);
                    return new OEECalculation
                    {
                        MachineId = machine.Id,
                        CalculationTime = DateTime.UtcNow,
                        Availability = 0,
                        Performance = 0,
                        Quality = 0,
                        OEE = 0,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd
                    };
                }

                // Validate TargetCycleTime (edge case: invalid/zero cycle time)
                var idealCycleTime = machine.TargetCycleTime;
                if (idealCycleTime <= 0)
                {
                    _logger.LogWarning("Invalid TargetCycleTime {Value} for machine {MachineId} - setting Performance to 0",
                        idealCycleTime, machine.Id);
                    idealCycleTime = 0;
                }

                // Calculate total time (planned production time)
                var totalTime = (periodEnd - periodStart).TotalSeconds;
                if (totalTime <= 0)
                {
                    _logger.LogWarning("Zero or negative total time for machine {MachineId}: {TotalTime} seconds",
                        machine.Id, totalTime);
                    return new OEECalculation
                    {
                        MachineId = machine.Id,
                        CalculationTime = DateTime.UtcNow,
                        Availability = 0,
                        Performance = 0,
                        Quality = 0,
                        OEE = 0,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd
                    };
                }

                // Calculate runtime (time between MachineStarted and MachineStopped/Maintenance/Idle)
                double runtime = 0;
                DateTime? lastStart = null;
                foreach (var evt in periodEvents)
                {
                    if (evt.EventType == EventType.MachineStarted)
                    {
                        lastStart = evt.Timestamp; // Start a runtime segment
                    }
                    else if ((evt.EventType == EventType.MachineStopped || evt.EventType == EventType.Maintenance || evt.EventType == EventType.Idle) && lastStart != null)
                    {
                        runtime += (evt.Timestamp - lastStart.Value).TotalSeconds; // End segment
                        lastStart = null;
                    }
                }
                if (lastStart != null && lastStart.Value <= periodEnd)
                {
                    runtime += (periodEnd - lastStart.Value).TotalSeconds; // Still running at period end
                }

                // Clamp runtime to 0–totalTime (handles timestamp errors)
                runtime = Math.Max(0, Math.Min(runtime, totalTime));
                _logger.LogDebug("Machine {MachineId}: Runtime = {Runtime} seconds, Total Time = {TotalTime} seconds",
                    machine.Id, runtime, totalTime);

                // Calculate Availability
                var availability = totalTime > 0 ? (runtime / totalTime) * 100 : 0;
                availability = Math.Max(0, Math.Min(100, availability)); // Clamp to 0–100
                _logger.LogDebug("Machine {MachineId}: Availability = {Availability}%", machine.Id, availability);

                // Calculate total production (sum ProductionCycle quantities, clamp >= 0)
                var totalProductionCount = periodEvents
                    .Where(e => e.EventType == EventType.ProductionCycle)
                    .Sum(e => Math.Max(0, e.Quantity ?? 0));
                _logger.LogDebug("Machine {MachineId}: Total Production = {TotalProductionCount}", machine.Id, totalProductionCount);

                // Calculate defective units (sum DefectiveUnit quantities, clamp >= 0)
                var defectiveSum = periodEvents
                    .Where(e => e.EventType == EventType.DefectiveUnit)
                    .Sum(e => Math.Max(0, e.Quantity ?? 0));
                _logger.LogDebug("Machine {MachineId}: Defective Units = {DefectiveSum}", machine.Id, defectiveSum);

                // Calculate Performance (edge case: zero runtime/production = 0)
                var performance = (runtime > 0 && totalProductionCount > 0 && idealCycleTime > 0)
                    ? (idealCycleTime * totalProductionCount / runtime) * 100
                    : 0;
                performance = Math.Max(0, Math.Min(100, performance)); // Clamp to 0–100
                _logger.LogDebug("Machine {MachineId}: Performance = {Performance}% (IdealCycleTime = {IdealCycleTime})",
                    machine.Id, performance, idealCycleTime);

                // Calculate Quality (edge case: zero production = 0)
                // Calculate Quality - FIXED VERSION
                var goodCount = Math.Max(0, totalProductionCount - defectiveSum); // Clamp >= 0
                _logger.LogDebug("Machine {MachineId}: Good Count = {GoodCount}, Defective Sum = {DefectiveSum}",
                    machine.Id, goodCount, defectiveSum);

                // FIXED: Handle quality calculation properly
                var quality = 0.0;
                if (totalProductionCount > 0)
                {
                    quality = (double)goodCount / totalProductionCount * 100;
                    quality = Math.Max(0, Math.Min(100, quality)); // Clamp to 0–100
                }
                _logger.LogDebug("Machine {MachineId}: Quality = {Quality}%", machine.Id, quality);

                // Calculate OEE
                var oee = (availability * performance * quality) / 10000;
                oee = Math.Max(0, Math.Min(100, oee)); // Clamp to 0–100
                _logger.LogDebug("Machine {MachineId}: OEE = {OEE}%", machine.Id, oee);

                // Generate alerts for low metrics (real-world: notify operators)
                var alerts = new List<string>();
                if (availability < 80) alerts.Add($"Low Availability: {Math.Round((decimal)availability, 2)}%");
                if (performance < 85) alerts.Add($"Low Performance: {Math.Round((decimal)performance, 2)}%");
                if (quality < 90) alerts.Add($"Low Quality: {Math.Round((decimal)quality, 2)}%");
                if (alerts.Any())
                {
                    _logger.LogInformation("Machine {MachineId}: Alerts triggered: {Alerts}",
                        machine.Id, string.Join(", ", alerts));
                }

                // Return result with rounded values for display
                return new OEECalculation
                {
                    MachineId = machine.Id,
                    CalculationTime = DateTime.UtcNow,
                    Availability = Math.Round((decimal)availability, 2),
                    Performance = Math.Round((decimal)performance, 2),
                    Quality = Math.Round((decimal)quality, 2),
                    OEE = Math.Round((decimal)oee, 2),
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd

                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OEE calculation for machine {MachineId}", machine.Id);
                throw new InvalidOperationException($"Failed to calculate OEE for machine {machine.Id}", ex);
            }
        }

        /// <summary>
        /// Calculates OEE for all machines in the specified period.
        /// Handles individual machine failures without stopping the batch.
        /// </summary>
        /// <param name="periodStart">Start of the period (UTC).</param>
        /// <param name="periodEnd">End of the period (UTC).</param>
        /// <returns>List of OEE calculations for all machines.</returns>
        public async Task<List<OEECalculation>> CalculateOEEForAllMachinesAsync(DateTime periodStart, DateTime periodEnd)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machines = await context.Machines.ToListAsync();
                var calculations = new List<OEECalculation>();

                foreach (var machine in machines)
                {
                    try
                    {
                        var calculation = await CalculateOEEAsync(machine.Id, periodStart, periodEnd);
                        calculations.Add(calculation);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to calculate OEE for machine {MachineId}", machine.Id);
                        // Continue to next machine (don't fail entire batch)
                    }
                }

                return calculations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating OEE for all machines from {PeriodStart} to {PeriodEnd}",
                    periodStart, periodEnd);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the latest OEE calculation for a machine.
        /// </summary>
        /// <param name="machineId">Machine ID.</param>
        /// <returns>Latest OEE calculation or null if none exists.</returns>
        public async Task<OEECalculation?> GetLatestOEECalculationAsync(int machineId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.OEECalculations
                    .Where(oc => oc.MachineId == machineId)
                    .OrderByDescending(oc => oc.CalculationTime)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest OEE calculation for machine {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the latest OEE calculations for multiple machines.
        /// </summary>
        /// <param name="machineIds">List of machine IDs.</param>
        /// <returns>Dictionary of machine IDs to their latest OEE calculations.</returns>
        public async Task<Dictionary<int, OEECalculation>> GetLatestOEECalculationsAsync(List<int> machineIds)
        {
            try
            {
                if (machineIds == null || !machineIds.Any())
                {
                    _logger.LogWarning("Empty or null machine IDs provided for latest OEE calculations");
                    return new Dictionary<int, OEECalculation>();
                }

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var calculations = await context.OEECalculations
                    .Where(oc => machineIds.Contains(oc.MachineId))
                    .GroupBy(oc => oc.MachineId)
                    .Select(g => g.OrderByDescending(oc => oc.CalculationTime).First())
                    .ToListAsync();

                return calculations.ToDictionary(c => c.MachineId, c => c);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest OEE calculations for machines");
                throw;
            }
        }

        /// <summary>
        /// Calculates OEE for the last hour up to now.
        /// </summary>
        /// <param name="machineId">Machine ID.</param>
        /// <returns>OEE calculation for the last hour.</returns>
        public async Task<OEECalculation> CalculateCurrentOEEAsync(int machineId)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await CalculateOEEAsync(machineId, now.AddHours(-1), now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating current OEE for machine {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Calculates OEE for a specific shift period.
        /// </summary>
        /// <param name="machineId">Machine ID.</param>
        /// <param name="shiftStart">Start of the shift (UTC).</param>
        /// <param name="shiftEnd">End of the shift (UTC).</param>
        /// <returns>OEE calculation for the shift.</returns>
        public async Task<OEECalculation> CalculateShiftOEEAsync(int machineId, DateTime shiftStart, DateTime shiftEnd)
        {
            try
            {
                return await CalculateOEEAsync(machineId, shiftStart, shiftEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating shift OEE for machine {MachineId} from {ShiftStart} to {ShiftEnd}",
                    machineId, shiftStart, shiftEnd);
                throw;
            }
        }

    }
}