
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModel;
using OEEMonitoringSystem.Models.ViewModels;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IMachineService _machineService;
        private readonly ITimezoneService _timezone;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportController(
            IReportService reportService,
            IMachineService machineService,
            ITimezoneService timezone,
            UserManager<ApplicationUser> userManager)
        {
            _reportService = reportService;
            _machineService = machineService;
            _timezone = timezone;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            //var viewModel = new ReportViewModel
            //{
            //    // Defaults in user's local time
            //    StartDate = DateTime.Today.AddDays(-7),
            //    EndDate = DateTime.Today,
            //    Machines = await _machineService.GetAllMachinesAsync()
            //};

            //return View(viewModel);
            var user = User.Identity?.Name; // logged-in username
            var isAdmin = User.IsInRole("Admin");
            var userId = _userManager.GetUserId(User);  // this gives AspNetUsers.Id (GUID string)

            var machines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId); // <-- only assigned machines

            var viewModel = new ReportViewModel
            {
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Today,
                Machines = machines
            };

            return View(viewModel);
        }




        //[HttpPost]
        //public async Task<IActionResult> Generate(ReportViewModel model)
        //{
        //    var startUtc = _timezone.ConvertUserTimeToUtc(model.StartDate);
        //    var endUtc = _timezone.ConvertUserTimeToUtc(model.EndDate);

        //    var calculationsUtc = await _reportService.GetOEECalculationsForReportAsync(
        //        startUtc, endUtc, model.SelectedMachineIds);

        //    foreach (var c in calculationsUtc)
        //    {
        //        c.PeriodStart = _timezone.ConvertUtcToUserTime(c.PeriodStart);
        //        c.PeriodEnd = _timezone.ConvertUtcToUserTime(c.PeriodEnd);
        //        c.CalculationTime = _timezone.ConvertUtcToUserTime(c.CalculationTime);
        //    }

        //    var user = User.Identity?.Name;
        //    var isAdmin = User.IsInRole("Admin");
        //    var userId = _userManager.GetUserId(User);
        //    model.Machines = isAdmin
        //        ? await _machineService.GetAllMachinesAsync()
        //        : await _machineService.GetMachinesForUserAsync(userId);

        //    model.OEECalculations = calculationsUtc;

        //    return View("Index", model);
        //}
        [HttpPost]
        public async Task<IActionResult> Generate(ReportViewModel model)
        {
            // Validate machine selection
            if (model.SelectedMachineIds == null || !model.SelectedMachineIds.Any())
            {
                ModelState.AddModelError("SelectedMachineIds", "Please select at least one machine before generating the report.");
            }

            var startUtc = _timezone.ConvertUserTimeToUtc(model.StartDate);
            var endUtc = _timezone.ConvertUserTimeToUtc(model.EndDate);

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Repopulate machines dropdown
            model.Machines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            if (!ModelState.IsValid)
            {
                // return back to view with validation error
                return View("Index", model);
            }

            // Handle "All Machines" option
            List<int> machineIds;
            if (model.SelectedMachineIds.Contains(-1)) // -1 means "All"
            {
                machineIds = model.Machines.Select(m => m.Id).ToList();
            }
            else
            {
                machineIds = model.SelectedMachineIds.ToList();
            }

            var calculationsUtc = await _reportService.GetOEECalculationsForReportAsync(
                startUtc, endUtc, machineIds);

            foreach (var c in calculationsUtc)
            {
                c.PeriodStart = _timezone.ConvertUtcToUserTime(c.PeriodStart);
                c.PeriodEnd = _timezone.ConvertUtcToUserTime(c.PeriodEnd);
                c.CalculationTime = _timezone.ConvertUtcToUserTime(c.CalculationTime);
            }

            model.OEECalculations = calculationsUtc;

            return View("Index", model);
        }



        [HttpGet]
        public async Task<IActionResult> ExportToExcel(DateTime startDate, DateTime endDate, string selectedMachineIds)
        {
            var machineIds = string.IsNullOrEmpty(selectedMachineIds)
                ? new List<int>()
                : selectedMachineIds.Split(',').Select(int.Parse).ToList();
            var user = User.Identity?.Name;
            var isAdmin = User.IsInRole("Admin");
            var userId = _userManager.GetUserId(User);
            var allowedMachines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            // ✅ filter to only allowed machines
            machineIds = machineIds.Where(id => allowedMachines.Any(m => m.Id == id)).ToList();

            var calculations = await _reportService.GetOEECalculationsForReportAsync(startDate, endDate, machineIds);

            if (!calculations.Any())
                return Content("No data found for the given filters.");

            var file = await _reportService.ExportToExcelAsync(calculations);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OEE_Report.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportToCsv(DateTime startDate, DateTime endDate, string selectedMachineIds)
        {
            var machineIds = string.IsNullOrEmpty(selectedMachineIds)
                ? new List<int>()
                : selectedMachineIds.Split(',').Select(int.Parse).ToList();
            var user = User.Identity?.Name;
            var isAdmin = User.IsInRole("Admin");
            var userId = _userManager.GetUserId(User);
            var allowedMachines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            // ✅ filter to only allowed machines
            machineIds = machineIds.Where(id => allowedMachines.Any(m => m.Id == id)).ToList();

            var calculations = await _reportService.GetOEECalculationsForReportAsync(startDate, endDate, machineIds);

            if (!calculations.Any())
                return Content("No data found for the given filters.");

            var file = await _reportService.ExportToCsvAsync(calculations);
            return File(file, "text/csv", "OEE_Report.csv");
        }

        [HttpGet]
        public async Task<IActionResult> WeeklyReport()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var machines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            var vm = new WeeklyReportViewModel
            {
                Month = DateTime.Today.Month,
                WeekNumber = 1,
                Machines = machines
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> WeeklyReport(WeeklyReportViewModel model)
        {
            if (!ModelState.IsValid || model.SelectedMachineIds == null || !model.SelectedMachineIds.Any())
            {
                ModelState.AddModelError("", "Please select at least one machine.");

                var userId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                model.Machines = isAdmin
                    ? await _machineService.GetAllMachinesAsync()
                    : await _machineService.GetMachinesForUserAsync(userId);

                return View(model);
            }

            // ✅ Calculate week start/end from month + week number
            var year = DateTime.Today.Year;
            var firstDayOfMonth = new DateTime(year, model.Month, 1);

            // Find Monday as first day of week
            var firstMonday = firstDayOfMonth.AddDays(((int)DayOfWeek.Monday - (int)firstDayOfMonth.DayOfWeek + 7) % 7);

            var weekStart = firstMonday.AddDays((model.WeekNumber - 1) * 7);
            var weekEnd = weekStart.AddDays(6);

            // Prevent going past the end of month
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            if (weekStart > lastDayOfMonth)
            {
                ModelState.AddModelError("", "Invalid week selection for this month.");

                var userId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                model.Machines = isAdmin
                    ? await _machineService.GetAllMachinesAsync()
                    : await _machineService.GetMachinesForUserAsync(userId);

                return View(model);
            }

            if (weekEnd > lastDayOfMonth)
                weekEnd = lastDayOfMonth;

            // ✅ Fetch weekly data
            model.Results = await _reportService.GetWeeklyOEEReportAsync(weekStart, weekEnd, model.SelectedMachineIds);

            var userId2 = _userManager.GetUserId(User);
            var isAdmin2 = User.IsInRole("Admin");

            model.Machines = isAdmin2
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId2);

            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> ExportWeeklyToCsv(int month, int weekNumber, string selectedMachineIds)
        {
            var machineIds = string.IsNullOrEmpty(selectedMachineIds)
                ? new List<int>()
                : selectedMachineIds.Split(',').Select(int.Parse).ToList();

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var allowedMachines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            machineIds = machineIds.Where(id => allowedMachines.Any(m => m.Id == id)).ToList();

            var startOfMonth = new DateTime(DateTime.Now.Year, month, 1);
            var firstDayOfWeek = startOfMonth.AddDays((weekNumber - 1) * 7);
            var lastDayOfWeek = firstDayOfWeek.AddDays(6);

            var weeklyData = await _reportService.GetWeeklyOEEReportAsync(firstDayOfWeek, lastDayOfWeek, machineIds);

            if (!weeklyData.Any())
                return Content("No data found for the selected week.");

            var file = await _reportService.ExportWeeklyToCsvAsync(weeklyData);
            return File(file, "text/csv", "Weekly_OEE_Report.csv");
        }

        // GET: Export Weekly Report Excel
        [HttpGet]
        public async Task<IActionResult> ExportWeeklyToExcel(int month, int weekNumber, string selectedMachineIds)
        {
            var machineIds = string.IsNullOrEmpty(selectedMachineIds)
                ? new List<int>()
                : selectedMachineIds.Split(',').Select(int.Parse).ToList();

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var allowedMachines = isAdmin
                ? await _machineService.GetAllMachinesAsync()
                : await _machineService.GetMachinesForUserAsync(userId);

            machineIds = machineIds.Where(id => allowedMachines.Any(m => m.Id == id)).ToList();

            var startOfMonth = new DateTime(DateTime.Now.Year, month, 1);
            var firstDayOfWeek = startOfMonth.AddDays((weekNumber - 1) * 7);
            var lastDayOfWeek = firstDayOfWeek.AddDays(6);

            var weeklyData = await _reportService.GetWeeklyOEEReportAsync(firstDayOfWeek, lastDayOfWeek, machineIds);

            if (!weeklyData.Any())
                return Content("No data found for the selected week.");

            var file = await _reportService.ExportWeeklyToExcelAsync(weeklyData);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Weekly_OEE_Report.xlsx");
        }

    }

}



