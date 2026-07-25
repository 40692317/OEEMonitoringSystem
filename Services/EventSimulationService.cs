using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Hubs;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace OEEMonitoringSystem.Services
{
    public class EventSimulationService : IEventSimulationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<OEEHub> _hubContext;
        private readonly IOEECalculationService _oeeCalculationService;
        private readonly IAlertService _alertService;
        private readonly IPredictionIntegrationService _predictionIntegration;
        private readonly ILogger<EventSimulationService> _logger;
        private readonly Random _random = new();
        private readonly Dictionary<int, bool> _machineStates = new();

        public EventSimulationService(
            IServiceProvider serviceProvider,
            IHubContext<OEEHub> hubContext,
            IOEECalculationService oeeCalculationService,
            IAlertService alertService,
            IPredictionIntegrationService predictionIntegration,
            ILogger<EventSimulationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _oeeCalculationService = oeeCalculationService ?? throw new ArgumentNullException(nameof(oeeCalculationService));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _predictionIntegration = predictionIntegration ?? throw new ArgumentNullException(nameof(predictionIntegration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Simulates realistic events for all active machines using state-aware logic.
        /// When a machine is running: 70% production, 10% defect, 10% stop, 10% maintenance.
        /// When a machine is stopped: 80% start, 20% maintenance.
        /// Called by BackgroundSimulationService on a timer.
        /// </summary>
        public async Task SimulateEventsForAllMachinesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machines = await context.Machines.Where(m => m.IsActive).ToListAsync();
                if (!machines.Any())
                {
                    _logger.LogDebug("No active machines found for simulation");
                    return;
                }

                foreach (var machine in machines)
                {
                    try
                    {
                        if (!_machineStates.ContainsKey(machine.Id))
                            _machineStates[machine.Id] = machine.CurrentState == "Running";

                        var (eventType, quantity) = PickStateAwareEvent(machine.Id);

                        if ((eventType == EventType.ProductionCycle || eventType == EventType.DefectiveUnit)
                            && !_machineStates[machine.Id])
                            continue;

                        await SimulateMachineEventAsync(machine.Id, eventType, DateTime.UtcNow, quantity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error simulating event for machine {MachineId}", machine.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch event simulation");
            }
        }

        /// <summary>
        /// Picks a realistic event type based on the machine's current running/stopped state.
        /// </summary>
        private (EventType eventType, int? quantity) PickStateAwareEvent(int machineId)
        {
            bool isRunning = _machineStates.ContainsKey(machineId) && _machineStates[machineId];
            int roll = _random.Next(100);

            if (isRunning)
            {
                if (roll < 70)
                    return (EventType.ProductionCycle, _random.Next(1, 6));
                if (roll < 80)
                    return (EventType.DefectiveUnit, _random.Next(1, 3));
                if (roll < 90)
                    return (EventType.MachineStopped, null);
                return (EventType.Maintenance, null);
            }

            return roll < 80
                ? (EventType.MachineStarted, null)
                : (EventType.Maintenance, null);
        }

        /// <summary>
        /// Simulates a random state-aware event for a single machine.
        /// Used by the manual "Simulate" button in the UI.
        /// </summary>
        public async Task SimulateRandomEventAsync(int machineId)
        {
            try
            {
                if (!_machineStates.ContainsKey(machineId))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var machine = await context.Machines.FindAsync(machineId);
                    _machineStates[machineId] = machine?.CurrentState == "Running";
                }

                var (eventType, quantity) = PickStateAwareEvent(machineId);
                await SimulateMachineEventAsync(machineId, eventType, DateTime.UtcNow, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating random event for machine {MachineId}", machineId);
            }
        }

        /// <summary>
        /// Convenience overload: simulates a specific event type with random quantity.
        /// </summary>
        public async Task SimulateMachineEventAsync(int machineId, EventType eventType)
        {
            int? quantity = null;
            if (eventType == EventType.ProductionCycle || eventType == EventType.DefectiveUnit)
                quantity = _random.Next(1, 5);

            await SimulateMachineEventAsync(machineId, eventType, DateTime.UtcNow, quantity);
        }

        /// <summary>
        /// Core simulation method. Creates the event, updates machine state, calculates OEE,
        /// persists only when OEE changes meaningfully, checks alerts, and broadcasts via SignalR.
        /// </summary>
        public async Task<bool> SimulateMachineEventAsync(
            int machineId,
            EventType eventType,
            DateTime timestamp,
            int? quantity = null,
            double? duration = null)
        {
            try
            {
                if (timestamp.Kind != DateTimeKind.Utc)
                    timestamp = timestamp.ToUniversalTime();

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machine = await context.Machines.FindAsync(machineId);
                if (machine == null)
                    throw new ArgumentException($"Machine with ID {machineId} not found");

                if (!_machineStates.ContainsKey(machine.Id))
                    _machineStates[machine.Id] = machine.CurrentState == "Running";

                // Validate production lockout (can't produce faster than TargetCycleTime)
                if (eventType == EventType.ProductionCycle)
                {
                    var lastProduction = await context.MachineEvents
                        .Where(e => e.MachineId == machineId && e.EventType == EventType.ProductionCycle)
                        .OrderByDescending(e => e.Timestamp)
                        .FirstOrDefaultAsync();

                    if (lastProduction != null && timestamp < lastProduction.Timestamp.AddSeconds(machine.TargetCycleTime))
                    {
                        _logger.LogDebug("ProductionCycle rejected for machine {MachineId}: still in cycle lockout", machineId);
                        return false;
                    }
                }

                // Can't produce or report defects when machine is stopped
                if ((eventType == EventType.ProductionCycle || eventType == EventType.DefectiveUnit)
                    && !_machineStates[machine.Id])
                {
                    _logger.LogDebug("{EventType} rejected for stopped machine {MachineId}", eventType, machineId);
                    return false;
                }

                // Clamp negative quantities
                if ((eventType == EventType.ProductionCycle || eventType == EventType.DefectiveUnit)
                    && quantity.HasValue && quantity.Value < 0)
                    quantity = 0;

                // When machine restarts, record downtime duration on the previous stop event
                if (eventType == EventType.MachineStarted)
                {
                    var lastDowntime = await context.MachineEvents
                        .Where(e => e.MachineId == machineId
                            && (e.EventType == EventType.MachineStopped || e.EventType == EventType.Maintenance || e.EventType == EventType.Idle)
                            && e.Timestamp < timestamp
                            && e.Duration == null)
                        .OrderByDescending(e => e.Timestamp)
                        .FirstOrDefaultAsync();

                    if (lastDowntime != null)
                    {
                        var downtimeSeconds = (timestamp - lastDowntime.Timestamp).TotalSeconds;
                        if (downtimeSeconds >= 0)
                        {
                            lastDowntime.Duration = downtimeSeconds;
                            context.MachineEvents.Update(lastDowntime);
                        }
                    }
                }

                // Create the event
                var machineEvent = new MachineEvent
                {
                    MachineId = machineId,
                    EventType = eventType,
                    Timestamp = timestamp,
                    Quantity = quantity,
                    Duration = (eventType != EventType.MachineStopped && eventType != EventType.Maintenance && eventType != EventType.Idle)
                        ? (duration.HasValue && duration.Value >= 0 ? duration : null)
                        : null
                };

                // Update machine running state
                if (eventType == EventType.MachineStarted)
                {
                    _machineStates[machine.Id] = true;
                    machine.CurrentState = "Running";
                }
                else if (eventType == EventType.MachineStopped || eventType == EventType.Maintenance || eventType == EventType.Idle)
                {
                    _machineStates[machine.Id] = false;
                    machine.CurrentState = eventType.ToString();
                }

                context.MachineEvents.Add(machineEvent);
                context.Update(machine);
                await context.SaveChangesAsync();

                _logger.LogInformation("Event: Machine {MachineId} {EventType} qty={Quantity}",
                    machineId, eventType, quantity);

                // Calculate OEE for rolling 1-hour window
                var currentOEE = await _oeeCalculationService.CalculateCurrentOEEAsync(machine.Id);

                // Only persist if OEE metrics changed meaningfully (>0.5% in any component)
                bool shouldPersist = await ShouldPersistOEEAsync(context, machine.Id, currentOEE);
                if (shouldPersist)
                {
                    context.OEECalculations.Add(currentOEE);
                    await context.SaveChangesAsync();
                    _logger.LogDebug("Persisted OEE for machine {MachineId}: A={A} P={P} Q={Q} OEE={OEE}",
                        machine.Id, currentOEE.Availability, currentOEE.Performance,
                        currentOEE.Quality, currentOEE.OEE);
                }

                await _alertService.CheckAndTriggerAlertsAsync(machine.Id);

                // Broadcast live update via SignalR
                var prediction = await _predictionIntegration.GetPredictionAsync(
                    machine.Id, currentOEE.PeriodStart, currentOEE.PeriodEnd);

                await _hubContext.Clients.Group($"Machine_{machine.Id}")
                    .SendAsync("OEEUpdated", new
                    {
                        MachineId = machine.Id,
                        OEE = (double)currentOEE.OEE,
                        Availability = (double)currentOEE.Availability,
                        Performance = (double)currentOEE.Performance,
                        Quality = (double)currentOEE.Quality,
                        MachineState = prediction.State.ToString(),
                        PredictedOEE = prediction.PredictedOEE,
                        Timestamp = DateTime.UtcNow,
                        EventType = eventType.ToString()
                    });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating {EventType} for machine {MachineId}", eventType, machineId);
                throw;
            }
        }

        /// <summary>
        /// Determines whether the new OEE calculation is different enough from the last
        /// persisted value to warrant saving. Avoids DB bloat from near-identical rows.
        /// Threshold: any component changed by more than 0.5%.
        /// </summary>
        private static async Task<bool> ShouldPersistOEEAsync(
            ApplicationDbContext context, int machineId, OEECalculation newCalc)
        {
            var lastSaved = await context.OEECalculations
                .Where(o => o.MachineId == machineId)
                .OrderByDescending(o => o.CalculationTime)
                .FirstOrDefaultAsync();

            if (lastSaved == null)
                return true;

            const decimal threshold = 0.5m;
            return Math.Abs(newCalc.Availability - lastSaved.Availability) > threshold
                || Math.Abs(newCalc.Performance - lastSaved.Performance) > threshold
                || Math.Abs(newCalc.Quality - lastSaved.Quality) > threshold
                || Math.Abs(newCalc.OEE - lastSaved.OEE) > threshold;
        }

        public async Task BroadcastOEEUpdateAsync(
            int machineId, double oee, double availability, double performance, double quality)
        {
            try
            {
                var now = DateTime.UtcNow;
                var prediction = await _predictionIntegration.GetPredictionAsync(machineId, now.AddHours(-1), now);
                await _hubContext.Clients.Group($"Machine_{machineId}")
                    .SendAsync("OEEUpdated", new
                    {
                        MachineId = machineId,
                        OEE = oee,
                        Availability = availability,
                        Performance = performance,
                        Quality = quality,
                        MachineState = prediction.State.ToString(),
                        PredictedOEE = prediction.PredictedOEE,
                        Timestamp = now
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting OEE update for machine {MachineId}", machineId);
            }
        }

        public async Task<List<Machine>> GetActiveMachinesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await context.Machines.Where(m => m.IsActive).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active machines");
                throw;
            }
        }
    }
}
