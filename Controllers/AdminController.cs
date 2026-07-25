using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModels;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMachineService _machineService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
              IMachineService machineService,
              ILogger<AdminController> logger
            )


        {
            // Add this using directive at the top of the file
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _machineService = machineService;
            _logger = logger;
        }
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var machines = await _machineService.GetAllMachinesAsync();

            ViewBag.Users = users;
            ViewBag.Machines = machines;

            return View();
        }

        public async Task<IActionResult> Machines()
        {
            var machines = await _context.Machines
                .Include(m => m.UserMachines)
                .OrderBy(m => m.Name)
                .ToListAsync();

            // Get all users (or filter if you only want specific roles)
            var users = await _userManager.Users.Where(u=>u.IsActive).ToListAsync();
            ViewBag.Users = users;

            var viewModel = new MachineListViewModel
            {
                Machines = machines
            };

            return View(viewModel);
        }


        public IActionResult CreateMachine()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMachine(MachineViewModel model)
        {
            if (ModelState.IsValid)
            {
                var machine = new Machine
                {
                    Name = model.Name,
                    Location = model.Location,
                    TargetCycleTime = model.TargetCycleTime,
                    IsActive = model.IsActive
                };

                _context.Machines.Add(machine);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Machines));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditMachine(int id)
        {
            var machine = await _machineService.GetMachineByIdAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            var assignedUsers = await _machineService.GetUsersAssignedToMachineAsync(id);
            var allUsers = await _machineService.GetAllUsersAsync();

            ViewBag.AssignedUsers = assignedUsers;
            ViewBag.AllUsers = allUsers;

            return View(machine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMachine(int id, Machine machine, List<string> selectedUserIds)
        {
            if (id != machine.Id)
            {
                return NotFound();
            }

            // Validate model state
            if (!ModelState.IsValid)
            {
                // Repopulate ViewBag for the view
                var assignedUsers = await _machineService.GetUsersAssignedToMachineAsync(id);
                var allUsers = await _machineService.GetAllUsersAsync();

                ViewBag.AssignedUsers = assignedUsers;
                ViewBag.AllUsers = allUsers;

                return View(machine);
            }

            try
            {
                // Get the existing machine from database
                var existingMachine = await _context.Machines.FindAsync(id);
                if (existingMachine == null)
                {
                    return NotFound();
                }

                // Explicitly update each property to avoid binding issues
                existingMachine.Name = machine.Name;
                existingMachine.Location = machine.Location;
                existingMachine.TargetCycleTime = machine.TargetCycleTime;
                existingMachine.IsActive = machine.IsActive;
                existingMachine.UpdatedAt = DateTime.UtcNow;

                // Save changes
                _context.Machines.Update(existingMachine);
                await _context.SaveChangesAsync();

                // Handle user assignments
                if (selectedUserIds != null)
                {
                    // Remove all existing assignments
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

                // Repopulate ViewBag for the view
                var assignedUsers = await _machineService.GetUsersAssignedToMachineAsync(id);
                var allUsers = await _machineService.GetAllUsersAsync();

                ViewBag.AssignedUsers = assignedUsers;
                ViewBag.AllUsers = allUsers;

                return View(machine);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMachine(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine != null)
            {
                _context.Machines.Remove(machine);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Machines));
        }

     

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, userRoles);
            await _userManager.AddToRoleAsync(user, role);

            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignMachineToUser(int machineId, string userId)
        {
            var existingAssignment = await _context.UserMachines
                .FirstOrDefaultAsync(um => um.MachineId == machineId && um.UserId == userId);

            if (existingAssignment == null)
            {
                var userMachine = new UserMachine
                {
                    MachineId = machineId,
                    UserId = userId
                };
                _context.UserMachines.Add(userMachine);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Machines));
        }

        public async Task<IActionResult> ManageUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoles = new Dictionary<string, IList<string>>();

            foreach (var user in users)
            {
                userRoles[user.Id] = await _userManager.GetRolesAsync(user);
            }

            ViewBag.UserRoles = userRoles;
            return View(users);
        }

        public async Task<IActionResult> CreateUser()
        {
            var allRoles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();
            ViewBag.AllRoles = allRoles;
            return View(new RegisterViewModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
          

                try
                {
                    // Create user with IdentityUser properties
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        IsActive = model.IsActive // Use the IsActive property from the model
                    };

                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        // Assign role if specified
                        if (!string.IsNullOrEmpty(Request.Form["role"]))
                        {
                            var role = Request.Form["role"].ToString();
                            await _userManager.AddToRoleAsync(user, role);
                        }

                        TempData["Success"] = "User created successfully!";
                        return RedirectToAction("ManageUsers");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating user {UserName}", model?.FirstName);
                    ModelState.AddModelError("", "Error creating user. Please try again.");
                }
            }

            var allRoles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();
            ViewBag.AllRoles = allRoles;
            return View(model);
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;
            var allRoles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();
            ViewBag.AllRoles = allRoles;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, ApplicationUser user, string[] selectedRoles)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _userManager.FindByIdAsync(id);
                    if (existingUser == null)
                    {
                        return NotFound();
                    }

                    // Update user properties
                    existingUser.FirstName = user.FirstName;
                    existingUser.LastName = user.LastName;
                    existingUser.Email = user.Email;
                    existingUser.UserName = user.UserName;
                    existingUser.IsActive = user.IsActive;

                    var result = await _userManager.UpdateAsync(existingUser);
                    if (result.Succeeded)
                    {
                        // Handle role assignments
                        if (selectedRoles != null)
                        {
                            var userRoles = await _userManager.GetRolesAsync(existingUser);
                            var removeResult = await _userManager.RemoveFromRolesAsync(existingUser, userRoles);

                            if (removeResult.Succeeded)
                            {
                                var addResult = await _userManager.AddToRolesAsync(existingUser, selectedRoles);
                                if (addResult.Succeeded)
                                {
                                    TempData["Success"] = "User updated successfully!";
                                    return RedirectToAction("ManageUsers");
                                }
                                else
                                {
                                    foreach (var error in addResult.Errors)
                                    {
                                        ModelState.AddModelError("", error.Description);
                                    }
                                }
                            }
                            else
                            {
                                foreach (var error in removeResult.Errors)
                                {
                                    ModelState.AddModelError("", error.Description);
                                }
                            }
                        }
                        else
                        {
                            TempData["Success"] = "User updated successfully!";
                            return RedirectToAction("ManageUsers");
                        }
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user {UserId}", id);
                    ModelState.AddModelError("", "Error updating user. Please try again.");
                }
            }

            ViewBag.UserRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(id));
            ViewBag.AllRoles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();

            return View(user);
        }
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id, ApplicationUser user)
        {
            try
            {
                var existingUser = await _userManager.FindByIdAsync(id);
                if (existingUser == null)
                {
                    return NotFound();
                }

                // Remove all user-machine assignments
                var userMachines = await _context.UserMachines.Where(um => um.UserId == id).ToListAsync();
                _context.UserMachines.RemoveRange(userMachines);
                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(existingUser);
                if (result.Succeeded)
                {
                    TempData["Success"] = "User deleted successfully!";
                    return RedirectToAction("ManageUsers");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                ModelState.AddModelError("", "Error deleting user. Please try again.");
            }

            return View(user);
        }

        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;

            var assignedMachines = await _context.UserMachines
                .Where(um => um.UserId == id)
                .Include(um => um.Machine)
                .Select(um => um.Machine)
                .ToListAsync();
            ViewBag.AssignedMachines = assignedMachines;
            ViewBag.IsLockedOut = await _userManager.IsLockedOutAsync(user);

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            ViewBag.UserEmail = user.Email;
            ViewBag.UserId = user.Id;
            ViewBag.UserName = $"{user.FirstName} {user.LastName}";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Password must be at least 6 characters.");
                ViewBag.UserEmail = user.Email;
                ViewBag.UserId = user.Id;
                ViewBag.UserName = $"{user.FirstName} {user.LastName}";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                ViewBag.UserEmail = user.Email;
                ViewBag.UserId = user.Id;
                ViewBag.UserName = $"{user.FirstName} {user.LastName}";
                return View();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Password reset successfully for {user.Email}.";
                return RedirectToAction(nameof(UserDetails), new { id });
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.UserEmail = user.Email;
            ViewBag.UserId = user.Id;
            ViewBag.UserName = $"{user.FirstName} {user.LastName}";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;

            if (!user.IsActive)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                user.LockoutEnd = DateTimeOffset.MaxValue;
            }
            else
            {
                user.LockoutEnd = null;
            }

            await _userManager.UpdateAsync(user);

            var status = user.IsActive ? "activated" : "revoked";
            TempData["Success"] = $"Account {status} for {user.Email}.";
            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            TempData["Success"] = $"Account unlocked for {user.Email}.";
            return RedirectToAction(nameof(UserDetails), new { id });
        }

        public async Task<IActionResult> ManageRoles()
        {
            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            var roleUserCount = new Dictionary<string, int>();
            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                roleUserCount[role.Id] = usersInRole?.Count ?? 0;
            }
            ViewBag.RoleUserCount = roleUserCount;
            return View(roles);
        }

        public IActionResult CreateRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "Role name is required.");
                return View();
            }
            name = name.Trim();
            if (await _roleManager.RoleExistsAsync(name))
            {
                ModelState.AddModelError("", $"Role '{name}' already exists.");
                return View();
            }
            var result = await _roleManager.CreateAsync(new IdentityRole(name));
            if (result.Succeeded)
            {
                TempData["Success"] = "Role created successfully.";
                return RedirectToAction(nameof(ManageRoles));
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View();
        }

        public async Task<IActionResult> EditRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();
            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(string id, string name)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "Role name is required.");
                return View(role);
            }
            name = name.Trim();
            if (role.Name != name && await _roleManager.RoleExistsAsync(name))
            {
                ModelState.AddModelError("", $"Role '{name}' already exists.");
                return View(role);
            }
            role.Name = name;
            role.NormalizedName = _roleManager.NormalizeKey(name);
            var result = await _roleManager.UpdateAsync(role);
            if (result.Succeeded)
            {
                TempData["Success"] = "Role updated successfully.";
                return RedirectToAction(nameof(ManageRoles));
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(role);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            ViewBag.UserCount = usersInRole?.Count ?? 0;
            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("DeleteRole")]
        public async Task<IActionResult> DeleteRoleConfirmed(string id)
        {
            var existingRole = await _roleManager.FindByIdAsync(id);
            if (existingRole == null)
                return NotFound();
            var usersInRole = await _userManager.GetUsersInRoleAsync(existingRole.Name);
            if (usersInRole != null && usersInRole.Count > 0)
            {
                foreach (var user in usersInRole)
                    await _userManager.RemoveFromRoleAsync(user, existingRole.Name);
            }
            var result = await _roleManager.DeleteAsync(existingRole);
            if (result.Succeeded)
            {
                TempData["Success"] = "Role deleted successfully.";
                return RedirectToAction(nameof(ManageRoles));
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            ViewBag.UserCount = usersInRole?.Count ?? 0;
            return View(existingRole);
        }
    }
}

