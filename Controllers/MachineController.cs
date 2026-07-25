
//using DocumentFormat.OpenXml.Features;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Services;
using OEEMonitoringSystem.Services.Interfaces;
using OEEMonitoringSystem.Models.Enums;

namespace OEEMonitoringSystem.Controllers
{
    [Authorize]
    public class MachineController : Controller
    {
        private readonly IMachineService _machineService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MachineController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEventSimulationService _eventSimulationService;

        public MachineController(
            IMachineService machineService,
            UserManager<ApplicationUser> userManager,
            ILogger<MachineController> logger,
            ApplicationDbContext context,
            IEventSimulationService eventSimulationService)
        {
            _machineService = machineService;
            _userManager = userManager;
            _logger = logger;
            _context = context;
            _eventSimulationService = eventSimulationService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
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
         

            //var machines = await _machineService.GetMachinesForUserAsync(user.Id);

            ViewBag.IsAdmin = User.IsInRole("Admin");
            ViewBag.User = user;

            return View(machines);
        }

        public async Task<IActionResult> Details(int id)
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
                return RedirectToAction("Index", "Home");
            }

            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            var latestOEE = await _machineService.GetMachineEventsAsync(id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

            ViewBag.Machine = machine;
            ViewBag.RecentEvents = latestOEE;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(machine);
        }
        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> IsMachineNameInUse(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Json(true);

            var exists = await _machineService.MachineExistsByNameAsync(name);
            return exists
                ? Json($"Machine name '{name}' is already in use.")
                : Json(true);
        }
            [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Machine machine)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _machineService.CreateMachineAsync(machine);
                    TempData["Success"] = "Machine created successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating machine {MachineName}", machine.Name);
                    ModelState.AddModelError("", "Error creating machine. Please try again.");
                }
            }

            return View(machine);
        }

        [Authorize(Roles = "Admin")]
        // Add to Edit action
        public async Task<IActionResult> EditMachine(int id)
        {
            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            // Get users assigned to this machine
            var assignedUsers = await _machineService.GetUsersAssignedToMachineAsync(id);

            // Get all users
            var allUsers = await _machineService.GetAllUsersAsync();
            //var nonAdminUsers = allUsers.Where(u => !await _userManager.IsInRoleAsync(u, "Admin")).ToList();

            // Assign to ViewBag
            ViewBag.AssignedUsers = assignedUsers.Where(u => u.IsActive);

            ViewBag.AllUsers = allUsers.Where(u => u.IsActive).ToList();  // ✅ Only active users


            return View(machine);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMachine(int id, Machine machine, List<string> selectedUserIds)
        {
            if (id != machine.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update machine
                    await _machineService.UpdateMachineAsync(machine);

                    // Handle user assignments
                    if (selectedUserIds != null)
                    {
                        // Remove all existing assignments for this machine
                        var existingAssignments = await _context.UserMachines
                            .Where(um => um.MachineId == id)
                            .ToListAsync();

                        _context.UserMachines.RemoveRange(existingAssignments);
                        await _context.SaveChangesAsync();

                        // Add new assignments
                        foreach (var userId in selectedUserIds)
                        {
                            var userMachine = new UserMachine
                            {
                                UserId = userId,
                                MachineId = id
                            };
                            _context.UserMachines.Add(userMachine);
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["Success"] = "Machine updated successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating machine {MachineId}", machine.Id);
                    ModelState.AddModelError("", "Error updating machine. Please try again.");
                }
            }

            // Re-populate user lists for view
            var assignedUsers = await _machineService.GetUsersAssignedToMachineAsync(id);
            var allUsers = await _machineService.GetAllUsersAsync();

            //ViewBag.AssignedUsers = assignedUsers;
            //ViewBag.AllUsers = allUsers;
            ViewBag.AssignedUsers = assignedUsers.Where(u => u.IsActive);

            ViewBag.AllUsers = allUsers.Where(u => u.IsActive).ToList();

            return View(machine);
        }
        



        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            return View(machine);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, Machine machine)
        {
            try
            {
                await _machineService.DeleteMachineAsync(id);
                TempData["Success"] = "Machine deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting machine {MachineId}", id);
                ModelState.AddModelError("", "Error deleting machine. Please try again.");
                return View(machine);
            }
        }

       
        public async Task<IActionResult> Simulate(int id)
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
                TempData["Error"] = "You are not authorized to simulate this machine.";
                return RedirectToAction("Index", "Home");
            }

            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            ViewBag.Machine = machine;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(machine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimulateEvent(int machineId, EventType eventType)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user is authorized for this machine
            var isAuthorized = await _machineService.IsUserAuthorizedForMachineAsync(user.Id, machineId);
            if (!isAuthorized && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "You are not authorized to simulate this machine.";
                return RedirectToAction("Index", "Home");
            }

            // Simulate the machine event
            await _eventSimulationService.SimulateMachineEventAsync(machineId, eventType);

            TempData["Success"] = "Event simulated successfully!";
            return RedirectToAction(nameof(Simulate), new { id = machineId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSimulate(int machineId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user is authorized for this machine
            var isAuthorized = await _machineService.IsUserAuthorizedForMachineAsync(user.Id, machineId);
            if (!isAuthorized && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "You are not authorized to simulate this machine.";
                return RedirectToAction("Index", "Home");
            }

            // Automatically simulate a random event for the machine
            await _eventSimulationService.SimulateRandomEventAsync(machineId);

            TempData["Success"] = "Automatic event generated!";
            return RedirectToAction(nameof(Simulate), new { id = machineId });
        }

        public async Task<IActionResult> Events(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }
            var isAuthorized = await _machineService.IsUserAuthorizedForMachineAsync(user.Id, id);
            if (!isAuthorized && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "You are not authorized to see the event of this machine.";
                return RedirectToAction("Index", "Home");
            }
            var events = await _machineService.GetMachineEventsAsync(id, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            ViewBag.Machine = machine;
            return View(events);
        }
        

    }
}