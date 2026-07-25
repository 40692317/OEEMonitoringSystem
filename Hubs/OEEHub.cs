using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Hubs
{
    [Authorize]
    public class OEEHub : Hub
    {
        private readonly IOEECalculationService _oeeCalculationService;
        private readonly IPredictionIntegrationService _predictionIntegration;

        public OEEHub(
            IOEECalculationService oeeCalculationService,
            IPredictionIntegrationService predictionIntegration)
        {
            _oeeCalculationService = oeeCalculationService;
            _predictionIntegration = predictionIntegration;
        }

        public async Task SubscribeToMachine(int machineId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Machine_{machineId}");
                await Clients.Caller.SendAsync("SubscriptionSuccess", new { MachineId = machineId, Status = "Subscribed" });

                var latestOEE = await _oeeCalculationService.GetLatestOEECalculationAsync(machineId);
                if (latestOEE != null)
                {
                    var prediction = await _predictionIntegration.GetPredictionAsync(machineId, latestOEE.PeriodStart, latestOEE.PeriodEnd);
                    await Clients.Caller.SendAsync("OEEUpdated", new
                    {
                        MachineId = machineId,
                        OEE = latestOEE.OEE,
                        Availability = latestOEE.Availability,
                        Performance = latestOEE.Performance,
                        Quality = latestOEE.Quality,
                        MachineState = prediction.State.ToString(),
                        PredictedOEE = prediction.PredictedOEE,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("SubscriptionError", new { MachineId = machineId, Error = ex.Message });
            }
        }

        public async Task UnsubscribeFromMachine(int machineId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Machine_{machineId}");
                await Clients.Caller.SendAsync("UnsubscriptionSuccess", new { MachineId = machineId, Status = "Unsubscribed" });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnsubscriptionError", new { MachineId = machineId, Error = ex.Message });
            }
        }
    }
}
