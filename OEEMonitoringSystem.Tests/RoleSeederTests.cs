using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Tests;

[TestFixture]
public class RoleSeederTests
{
    private const string AdminEmail = "admin@example.test";
    private const string ConfiguredPassword = "Configured#Pass1";

    private Mock<RoleManager<IdentityRole>> _roleManager = null!;
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private ListLogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _roleManager = new Mock<RoleManager<IdentityRole>>(
            Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
        _roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        _userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _userManager.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("token");
        _userManager.Setup(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _logger = new ListLogger();
    }

    private static IConfiguration Config(string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RoleSeeder.EmailKey] = AdminEmail,
                [RoleSeeder.PasswordKey] = password
            })
            .Build();

    private Task Seed(string? password) =>
        RoleSeeder.SeedAsync(_roleManager.Object, _userManager.Object, Config(password), _logger);

    [Test]
    public async Task NoPassword_AdminMissing_DoesNotCreateAdmin_AndLogsWarning()
    {
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync((ApplicationUser?)null);

        await Seed(password: null);

        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        Assert.That(_logger.Entries, Has.Some.Matches<(LogLevel Level, string Message)>(
            e => e.Level == LogLevel.Warning && e.Message.Contains(RoleSeeder.PasswordKey)));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task BlankPassword_TreatedAsMissing(string password)
    {
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync((ApplicationUser?)null);

        await Seed(password);

        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PasswordConfigured_AdminMissing_CreatesAdminWithConfiguredPassword()
    {
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync((ApplicationUser?)null);

        await Seed(ConfiguredPassword);

        _userManager.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(a => a.Email == AdminEmail && a.UserName == AdminEmail),
            ConfiguredPassword), Times.Once);
        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"), Times.Once);
    }

    [Test]
    public async Task PasswordConfigured_AdminExists_ResetsToConfiguredPassword()
    {
        var existing = new ApplicationUser { Email = AdminEmail, UserName = AdminEmail };
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync(existing);

        await Seed(ConfiguredPassword);

        _userManager.Verify(u => u.ResetPasswordAsync(existing, "token", ConfiguredPassword), Times.Once);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task NoPassword_AdminExists_LeavesPasswordUnchanged()
    {
        var existing = new ApplicationUser { Email = AdminEmail, UserName = AdminEmail };
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync(existing);

        await Seed(password: null);

        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CreateFails_LogsErrorWithoutPassword()
    {
        _userManager.Setup(u => u.FindByEmailAsync(AdminEmail)).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Passwords must have at least one digit." }));

        await Seed(ConfiguredPassword);

        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        Assert.That(_logger.Entries, Has.Some.Matches<(LogLevel Level, string Message)>(
            e => e.Level == LogLevel.Error && e.Message.Contains("at least one digit")));
        Assert.That(_logger.Entries.Select(e => e.Message), Has.None.Contains(ConfiguredPassword));
    }

    [Test]
    public void GetAdminEmail_NotConfigured_ReturnsDefault()
    {
        var empty = new ConfigurationBuilder().Build();

        Assert.That(RoleSeeder.GetAdminEmail(empty), Is.EqualTo(RoleSeeder.DefaultAdminEmail));
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
