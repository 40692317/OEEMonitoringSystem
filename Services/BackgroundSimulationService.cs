using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    /// <summary>
    /// Single background timer that drives event simulation for all machines.
    /// Interval is configurable via appsettings.json → BackgroundSimulation:IntervalSeconds.
    /// Resolves IEventSimulationService per cycle to call SimulateEventsForAllMachinesAsync().
    /// </summary>
    public class BackgroundSimulationService : IHostedService, IDisposable
    {
        private readonly ILogger<BackgroundSimulationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval;
        private Timer? _timer;
        private CancellationTokenSource? _cts;

        public BackgroundSimulationService(
            ILogger<BackgroundSimulationService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            var seconds = configuration.GetValue("BackgroundSimulation:IntervalSeconds", 5);
            if (seconds < 1) seconds = 1;
            _interval = TimeSpan.FromSeconds(seconds);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _interval);
            _logger.LogInformation("Background simulation started — interval {Seconds}s", _interval.TotalSeconds);
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            if (_cts?.IsCancellationRequested == true)
                return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sim = scope.ServiceProvider.GetRequiredService<IEventSimulationService>();
                await sim.SimulateEventsForAllMachinesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background simulation cycle");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background simulation stopping");
            _cts?.Cancel();
            _timer?.Dispose();
            _cts?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _cts?.Dispose();
        }
    }
}
