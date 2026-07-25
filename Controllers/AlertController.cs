using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModels;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Controllers
{
    [Authorize]
    public class AlertController : Controller
    {
        private readonly IAlertService _alertService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AlertController(
            IAlertService alertService,
            UserManager<ApplicationUser> userManager)
        {
            _alertService = alertService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var userRoles = await _userManager.GetRolesAsync(user);

            // Get all active alerts for display
            var allAlerts = await _alertService.GetActiveAlertsAsync();

            // Limit to 3 most recent for dashboard preview
            var recentAlerts = allAlerts.OrderByDescending(a => a.Timestamp)
                                       .Take(3)
                                       .ToList();

            // Calculate statistics
            var totalAlerts = allAlerts.Count;
            var resolvedAlerts = allAlerts.Count(a => a.IsResolved);
            var activeAlerts = allAlerts.Count(a => !a.IsResolved);

            var viewModel = new AlertViewModel
            {
                RecentAlerts = recentAlerts,
                AllAlerts = allAlerts,
                TotalAlerts = totalAlerts,
                ResolvedAlerts = resolvedAlerts,
                ActiveAlerts = activeAlerts
            };

            return View(viewModel);
        }

        public async Task<IActionResult> AllAlerts()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var userRoles = await _userManager.GetRolesAsync(user);

            // Get all alerts sorted by timestamp descending
            var allAlerts = await _alertService.GetActiveAlertsAsync();

            // Group alerts by date for better organization
            var groupedAlerts = allAlerts
                .GroupBy(a => a.Timestamp.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DateGroupedAlerts
                {
                    Date = g.Key,
                    Alerts = g.OrderBy(a => a.Timestamp).ToList()
                })
                .ToList();

            // Calculate statistics
            var totalAlerts = allAlerts.Count;
            var resolvedAlerts = allAlerts.Count(a => a.IsResolved);
            var activeAlerts = allAlerts.Count(a => !a.IsResolved);

            var viewModel = new AlertViewModel
            {
                GroupedAlerts = groupedAlerts,
                TotalAlerts = totalAlerts,
                ResolvedAlerts = resolvedAlerts,
                ActiveAlerts = activeAlerts
            };

            return View(viewModel);
        }

        public async Task<IActionResult> AlertHistory(int machineId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var userRoles = await _userManager.GetRolesAsync(user);

            // Get alert history (both active and resolved)
            var allAlerts = await _alertService.GetAlertHistoryAsync(machineId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            // Group alerts by date for better organization
            var groupedAlerts = allAlerts
                .GroupBy(a => a.Timestamp.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DateGroupedAlerts
                {
                    Date = g.Key,
                    Alerts = g.OrderBy(a => a.Timestamp).ToList()
                })
                .ToList();

            // Calculate statistics
            var totalAlerts = allAlerts.Count;
            var resolvedAlerts = allAlerts.Count(a => a.IsResolved);
            var activeAlerts = allAlerts.Count(a => !a.IsResolved);

            var viewModel = new AlertViewModel
            {
                GroupedAlerts = groupedAlerts,
                TotalAlerts = totalAlerts,
                ResolvedAlerts = resolvedAlerts,
                ActiveAlerts = activeAlerts
            };

            return View(viewModel);
        }
    }
}