
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModels;
using OEEMonitoringSystem.Services.Interfaces;
using OEEMonitoringSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;


namespace OEEMonitoringSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IMachineService _machineService;
        private readonly IOEECalculationService _oeeCalculationService;
        private readonly IAlertService _alertService;
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEventSimulationService _eventSimulationService;
        private readonly ILogger<DashboardController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IClassificationModelService _classificationModelService;
        private readonly IPredictionIntegrationService _predictionIntegration;

        public DashboardController(
            IMachineService machineService,
            IOEECalculationService oeeCalculationService,
            IAlertService alertService,
            IReportService reportService,
            UserManager<ApplicationUser> userManager,
            IEventSimulationService eventSimulationService,
            ILogger<DashboardController> logger,
            ApplicationDbContext context,
            IClassificationModelService classificationModelService,
            IPredictionIntegrationService predictionIntegration)
        {
            _machineService = machineService;
            _oeeCalculationService = oeeCalculationService;
            _alertService = alertService;
            _reportService = reportService;
            _userManager = userManager;
            _eventSimulationService = eventSimulationService;
            _logger = logger;
            _context = context;
            _classificationModelService = classificationModelService;
            _predictionIntegration = predictionIntegration;
        }
        // Add this to ensure proper initialization
        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Loading dashboard index page");

                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");
                var userRoles = await _userManager.GetRolesAsync(user);

                List<Machine> machines;
                if (userRoles.Contains("Admin"))
                {
                    machines = await _machineService.GetAllMachinesAsync();
                }
                else
                {
                    machines = await _machineService.GetMachinesForUserAsync(user.Id);
                }

                var machineIds = machines.Select(m => m.Id).ToList();
                var latestOEECalculations = await _oeeCalculationService.GetLatestOEECalculationsAsync(machineIds);

                // Get active alerts (limit to 3 most recent)
                var activeAlerts = await _alertService.GetActiveAlertsAsync();
                var limitedAlerts = activeAlerts.OrderByDescending(a => a.Timestamp)
                                               .Take(3)
                                               .ToList();

                ViewBag.Machines = machines;
                ViewBag.LatestOEECalculations = latestOEECalculations;
                ViewBag.IsAdmin = userRoles.Contains("Admin");
                ViewBag.ActiveAlerts = limitedAlerts;
                ViewBag.TotalAlerts = activeAlerts.Count;

                _logger.LogInformation("Dashboard loaded successfully with {MachineCount} machines", machines.Count);
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                TempData["Error"] = "Error loading dashboard: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }
     

        public async Task<IActionResult> PlantDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var userRoles = await _userManager.GetRolesAsync(user);

            List<Machine> machines;
            if (userRoles.Contains("Admin"))
            {
                machines = await _machineService.GetAllMachinesAsync();
            }
            else
            {
                machines = await _machineService.GetMachinesForUserAsync(user.Id);
            }

            var machineIds = machines.Select(m => m.Id).ToList();
            var latestOEECalculations = await _oeeCalculationService.GetLatestOEECalculationsAsync(machineIds);

            // Get active alerts
            var activeAlerts = await _alertService.GetActiveAlertsAsync();
            machines = machines.Where(m => m.IsActive).ToList();

            var machineStates = new Dictionary<int, MachineState>();
            var predictedOEE = new Dictionary<int, float?>();
            foreach (var m in machines)
            {
                if (latestOEECalculations.TryGetValue(m.Id, out var calc))
                {
                    var pred = await _predictionIntegration.GetPredictionAsync(m.Id, calc.PeriodStart, calc.PeriodEnd);
                    machineStates[m.Id] = pred.State;
                    predictedOEE[m.Id] = pred.PredictedOEE;
                }
                else
                {
                    machineStates[m.Id] = MachineState.Normal;
                    predictedOEE[m.Id] = null;
                }
            }

            ViewBag.Machines = machines;
            ViewBag.LatestOEECalculations = latestOEECalculations;
            ViewBag.MachineStates = machineStates;
            ViewBag.PredictedOEE = predictedOEE;
            ViewBag.IsAdmin = userRoles.Contains("Admin");
            ViewBag.ActiveAlerts = activeAlerts;

            return View("PlantDashboard");
        }


        public async Task<IActionResult> ChartDashboard(int? machineId, int? days)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var userRoles = await _userManager.GetRolesAsync(user);

            List<Machine> machines;
            if (userRoles.Contains("Admin"))
                machines = await _machineService.GetAllMachinesAsync();
            else
                machines = await _machineService.GetMachinesForUserAsync(user.Id);

            machines = machines.Where(m => m.IsActive).ToList();
            var selectedMachineId = machineId ?? machines.FirstOrDefault()?.Id;
            var rangeDays = days ?? 14;

            var chartData = selectedMachineId != null
                ? await GetChartDataAsync(new List<int> { selectedMachineId.Value }, rangeDays)
                : new ChartDataViewModel();

            ViewBag.Machines = machines;
            ViewBag.SelectedMachineId = selectedMachineId;
            ViewBag.SelectedDays = rangeDays;
            ViewBag.ChartData = chartData;
            ViewBag.IsAdmin = userRoles.Contains("Admin");

            return View("ChartDashboard");
        }

        private async Task<ChartDataViewModel> GetChartDataAsync(List<int> machineIds, int days = 14)
        {
            var chartData = new ChartDataViewModel();

            if (!machineIds.Any())
                return chartData;

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);

            var oeeRecords = await _reportService.GetOEECalculationsAsync(startDate, endDate);
            var filtered = oeeRecords.Where(o => machineIds.Contains(o.MachineId)).ToList();

            if (!filtered.Any())
                return chartData;

            var productionEvents = await _context.MachineEvents
                .Where(e => machineIds.Contains(e.MachineId)
                         && e.EventType == EventType.ProductionCycle
                         && e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToListAsync();

            var defectEvents = await _context.MachineEvents
                .Where(e => machineIds.Contains(e.MachineId)
                         && e.EventType == EventType.DefectiveUnit
                         && e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToListAsync();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var dayRecords = filtered.Where(o => o.PeriodStart.Date == date).ToList();

                chartData.Labels.Add(date.ToString("MMM dd"));

                if (dayRecords.Any())
                {
                    chartData.OEEData.Add(Math.Round(dayRecords.Average(o => (double)o.OEE), 2));
                    chartData.AvailabilityData.Add(Math.Round(dayRecords.Average(o => (double)o.Availability), 2));
                    chartData.PerformanceData.Add(Math.Round(dayRecords.Average(o => (double)o.Performance), 2));
                    chartData.QualityData.Add(Math.Round(dayRecords.Average(o => (double)o.Quality), 2));

                    double dailyDowntime = 0;
                    foreach (var rec in dayRecords)
                    {
                        if (rec.PeriodEnd <= rec.PeriodStart) continue;
                        var mins = (rec.PeriodEnd - rec.PeriodStart).TotalMinutes;
                        dailyDowntime += (double)(100 - rec.Availability) / 100.0 * mins;
                    }
                    chartData.DowntimeData.Add(Math.Round(Math.Min(dailyDowntime, 1440), 2));
                }
                else
                {
                    chartData.OEEData.Add(0);
                    chartData.AvailabilityData.Add(0);
                    chartData.PerformanceData.Add(0);
                    chartData.QualityData.Add(0);
                    chartData.DowntimeData.Add(0);
                }

                chartData.ThroughputData.Add(productionEvents
                    .Where(e => e.Timestamp.Date == date).Sum(e => e.Quantity ?? 0));
                chartData.DefectData.Add(defectEvents
                    .Where(e => e.Timestamp.Date == date).Sum(e => e.Quantity ?? 0));
            }

            chartData.ThroughputLabels = chartData.Labels;
            chartData.OEETrendLabels = chartData.Labels;
            chartData.OEETrendData = chartData.OEEData;
            chartData.DowntimeLabels = chartData.Labels;

            return chartData;
        }

        public async Task<IActionResult> MachineDetail(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user is authorized for this machine
            var isAuthorized = await _machineService.IsUserAuthorizedForMachineAsync(user.Id, id);
            if (!isAuthorized && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "You are not authorized to view this machine.";
                return RedirectToAction("Index");
            }

            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            var latestOEE = await _oeeCalculationService.GetLatestOEECalculationAsync(id);
            var recentEvents = await _machineService.GetMachineEventsAsync(id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

            MachineState mlState = MachineState.Normal;
            float? predictedOEE = null;
            if (latestOEE != null)
            {
                var pred = await _predictionIntegration.GetPredictionAsync(id, latestOEE.PeriodStart, latestOEE.PeriodEnd);
                mlState = pred.State;
                predictedOEE = pred.PredictedOEE;
            }

            ViewBag.Machine = machine;
            ViewBag.LatestOEE = latestOEE;
            ViewBag.RecentEvents = recentEvents;
            ViewBag.MachineState = mlState;
            ViewBag.PredictedOEE = predictedOEE;

            return View();
        }
    }
}