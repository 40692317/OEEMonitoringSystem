

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Hubs;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Services;
using OEEMonitoringSystem.Services.Interfaces;
using Serilog;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

// Ensure that the logs directory exists
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory); // Creates the directory if it doesn't exist
}

// Get the current date and format it for the log file name (e.g., logs_2025-08-27.txt)
string currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
string logFilePath = Path.Combine(logDirectory, $"logs_{currentDate}.txt");

// Set up Serilog: file + console so logs show in the window that opens when you run the app
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Infinite)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    // Database Setup
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Identity Setup
    builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
    // 🔹 Force Identity to use your custom error handling
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.AccessDeniedPath = "/Error/403";   // use your ErrorController
    });

    // SignalR Setup
    builder.Services.AddSignalR();

    // Custom Services Setup
    builder.Services.AddScoped<ITimezoneService, TimezoneService>();
    builder.Services.AddScoped<IMachineService, MachineService>();
    builder.Services.AddScoped<IOEECalculationService, OEECalculationService>();
    builder.Services.AddScoped<IEventSimulationService, EventSimulationService>();
    builder.Services.AddScoped<IAlertService, AlertService>();
    builder.Services.AddScoped<IReportService, ReportService>();
    builder.Services.AddScoped<IFeatureService, FeatureService>();
    builder.Services.AddScoped<IDatasetImportService, DatasetImportService>();
    builder.Services.AddScoped<IRegressionModelService, RegressionModelService>();
    builder.Services.AddScoped<IClassificationModelService, ClassificationModelService>();
    builder.Services.AddScoped<IPredictionIntegrationService, PredictionIntegrationService>();

    //Background Simulation Service
    builder.Services.AddHostedService<BackgroundSimulationService>();

    builder.Services.AddControllersWithViews();
    builder.Services.AddLogging();

    var app = builder.Build();

    // Logging for startup success
    Log.Information("Application started successfully at {Time}", DateTime.UtcNow);

    // Configure Middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseStatusCodePagesWithReExecute("/Error/{0}");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapRazorPages();
    app.MapHub<OEEHub>("/oeehub");

    // Seed roles and admin user
    using (var scope = app.Services.CreateScope())
    {
        await RoleSeeder.SeedAsync(scope.ServiceProvider);
    }

    // Seed demo data (machines, events, OEE, alerts) for admin
    await DataSeeder.SeedAsync(app.Services);

    // Log when application stops
    app.Lifetime.ApplicationStopped.Register(() =>
    {
        Log.Information("Application stopped at {Time}", DateTime.UtcNow);
    });

    app.Run();
}
catch (Exception ex)
{
    // Log any errors during app startup
    Log.Fatal(ex, "Application startup failed at {Time}", DateTime.UtcNow);
    throw;
}
finally
{
    Log.CloseAndFlush();
}
