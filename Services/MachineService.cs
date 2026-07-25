using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class MachineService : IMachineService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MachineService> _logger;
        ApplicationDbContext _context;

        public MachineService(
            IServiceProvider serviceProvider,
            ILogger<MachineService> logger,
            ApplicationDbContext context)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _context = context;
        }

        public async Task<List<Machine>> GetAllMachinesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machines = await context.Machines
                    .Include(m => m.UserMachines)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {MachineCount} machines from database", machines.Count);
                return machines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all machines");
                throw;
            }
        }

        public async Task<List<Machine>> GetMachinesForUserAsync(string userId)
        {
            // apply check if userid is admin and return all machines if true


            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machines = await context.Machines
                    .Include(m => m.UserMachines)
                    .Where(m => m.UserMachines.Any(um => um.UserId == userId))
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {MachineCount} machines for user {UserId}", machines.Count, userId);
                return machines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving machines for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Machine?> GetMachineByIdAsync(int id)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machine = await context.Machines
                    .Include(m => m.UserMachines)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (machine == null)
                {
                    _logger.LogWarning("Machine with ID {MachineId} not found", id);
                }
                else
                {
                    _logger.LogDebug("Retrieved machine {MachineName} (ID: {MachineId})", machine.Name, machine.Id);
                }

                return machine;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving machine {MachineId}", id);
                throw;
            }
        }

        public async Task<Machine> CreateMachineAsync(Machine machine)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                machine.CreatedAt = DateTime.UtcNow;
                context.Machines.Add(machine);
                await context.SaveChangesAsync();

                _logger.LogInformation("Created new machine {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);
                return machine;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating machine {MachineName}", machine?.Name ?? "Unknown");
                throw;
            }
        }

        public async Task<Machine> UpdateMachineAsync(Machine machine)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                machine.UpdatedAt = DateTime.UtcNow;
                context.Machines.Update(machine);
                await context.SaveChangesAsync();

                _logger.LogInformation("Updated machine {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);
                return machine;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating machine {MachineId}", machine?.Id ?? 0);
                throw;
            }
        }

        public async Task<bool> DeleteMachineAsync(int id)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var machine = await context.Machines.FindAsync(id);
                if (machine == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent machine {MachineId}", id);
                    return false;
                }

                context.Machines.Remove(machine);
                await context.SaveChangesAsync();

                _logger.LogInformation("Deleted machine {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting machine {MachineId}", id);
                throw;
            }
        }

        public async Task AssignMachineToUserAsync(int machineId, string userId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var existingAssignment = await context.UserMachines
                    .FirstOrDefaultAsync(um => um.MachineId == machineId && um.UserId == userId);

                if (existingAssignment == null)
                {
                    var userMachine = new UserMachine
                    {
                        MachineId = machineId,
                        UserId = userId
                    };
                    context.UserMachines.Add(userMachine);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Assigned machine {MachineId} to user {UserId}", machineId, userId);
                }
                else
                {
                    _logger.LogDebug("Machine {MachineId} already assigned to user {UserId}", machineId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning machine {MachineId} to user {UserId}", machineId, userId);
                throw;
            }
        }



        public async Task<List<MachineEvent>> GetMachineEventsAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var events = await context.MachineEvents
                    .Where(e => e.MachineId == machineId && e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {EventCount} events for machine {MachineId} from {StartDate} to {EndDate}",
                    events.Count, machineId, startDate, endDate);
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events for machine {MachineId}", machineId);
                throw;
            }
        }

        public async Task RemoveMachineFromUserAsync(string userId, int machineId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var userMachine = await context.UserMachines
                    .FirstOrDefaultAsync(um => um.UserId == userId && um.MachineId == machineId);

                if (userMachine != null)
                {
                    context.UserMachines.Remove(userMachine);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Removed machine {MachineId} from user {UserId}", machineId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing machine {MachineId} from user {UserId}", machineId, userId);
                throw;
            }
        }

        public async Task<bool> IsUserAuthorizedForMachineAsync(string userId, int machineId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.UserMachines
                    .AnyAsync(um => um.UserId == userId && um.MachineId == machineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authorization for user {UserId} and machine {MachineId}", userId, machineId);
                return false;
            }
        }
    
        // Add these methods to the existing implementation
        public async Task<List<ApplicationUser>> GetUsersAssignedToMachineAsync(int machineId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.Users
                    .Join(context.UserMachines.Where(um => um.MachineId == machineId),
                          u => u.Id,
                          um => um.UserId,
                          (u, um) => u)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users assigned to machine {MachineId}", machineId);
                return new List<ApplicationUser>();
            }
        }

        public async Task<List<ApplicationUser>> GetAllUsersAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.Users.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return new List<ApplicationUser>();
            }
        }
        public async Task<bool> MachineExistsByNameAsync(string name)
{
    return await _context.Machines.AnyAsync(m => m.Name == name);
}

    }
}