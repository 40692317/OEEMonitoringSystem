using Microsoft.AspNetCore.Identity;
using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Data
{
    public static class RoleSeeder
    {
        // Configuration keys. As environment variables: SeedAdmin__Email and SeedAdmin__Password.
        public const string EmailKey = "SeedAdmin:Email";
        public const string PasswordKey = "SeedAdmin:Password";
        public const string DefaultAdminEmail = "admin@gmail.com";

        public static string GetAdminEmail(IConfiguration configuration)
        {
            var email = configuration[EmailKey];
            return string.IsNullOrWhiteSpace(email) ? DefaultAdminEmail : email.Trim();
        }

        public static Task SeedAsync(IServiceProvider serviceProvider) =>
            SeedAsync(
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                serviceProvider.GetRequiredService<IConfiguration>(),
                serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RoleSeeder)));

        public static async Task SeedAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            string[] roleNames = { "Admin", "Operator" };

            // Create roles
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var adminEmail = GetAdminEmail(configuration);
            var adminPassword = configuration[PasswordKey];
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                if (adminUser == null)
                {
                    logger.LogWarning(
                        "{Key} is not set, so no admin account was created and no demo data will be seeded. " +
                        "Set it (environment variable SeedAdmin__Password) and restart.", PasswordKey);
                }
                return;
            }

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User"
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Admin account {Email} created.", adminEmail);
                }
                else
                {
                    LogFailure(logger, "create the admin account", result);
                }
            }
            else
            {
                // Keeps the admin password in sync with configuration, so changing the setting rotates it.
                var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                var result = await userManager.ResetPasswordAsync(adminUser, token, adminPassword);
                if (!result.Succeeded)
                {
                    LogFailure(logger, "update the admin password", result);
                }
            }
        }

        private static void LogFailure(ILogger logger, string action, IdentityResult result) =>
            logger.LogError("Could not {Action}: {Errors}", action,
                string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
