using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services;
using OEEMonitoringSystem.Hubs;
using NUnit.Framework;

namespace OEEMonitoringSystem.Tests;

[TestFixture]
public class OEECalculationServiceTests
{
    private OEECalculationService _service = null!;
    private Mock<ILogger<OEECalculationService>> _loggerMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<IHubContext<OEEHub>> _hubContextMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<OEECalculationService>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _hubContextMock = new Mock<IHubContext<OEEHub>>();
        _service = new OEECalculationService(
            _serviceProviderMock.Object,
            _loggerMock.Object,
            _hubContextMock.Object);
    }

    private static Machine Machine(int id, double targetCycleTime = 10) => new()
    {
        Id = id,
        Name = "M1",
        Location = "L1",
        TargetCycleTime = targetCycleTime,
        IsActive = true
    };

    private static MachineEvent Evt(EventType type, DateTime timestamp, int? quantity = null, double? duration = null) => new()
    {
        Id = 0,
        MachineId = 1,
        EventType = type,
        Timestamp = timestamp,
        Quantity = quantity,
        Duration = duration
    };

    [Test]
    public async Task CalculateOEE_InvalidPeriod_StartAfterEnd_ReturnsAllZeros()
    {
        var machine = Machine(1);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent> { Evt(EventType.MachineStarted, start) };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(0));
        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(0));
        Assert.That(result.OEE, Is.EqualTo(0));
        Assert.That(result.MachineId, Is.EqualTo(1));
    }

    [Test]
    public async Task CalculateOEE_NoEvents_ReturnsAllZeros()
    {
        var machine = Machine(1);
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var result = await _service.CalculateOEEAsync(machine, new List<MachineEvent>(), start, end);

        Assert.That(result.Availability, Is.EqualTo(0));
        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(0));
        Assert.That(result.OEE, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_EventsOutsidePeriod_ReturnsAllZeros()
    {
        var machine = Machine(1);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc)),
            Evt(EventType.MachineStopped, new DateTime(2025, 1, 1, 8, 30, 0, DateTimeKind.Utc))
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(0));
        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(0));
        Assert.That(result.OEE, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_ZeroTargetCycleTime_PerformanceZero_OthersCalculated()
    {
        var machine = Machine(1, 0);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(10), 10),
            Evt(EventType.MachineStopped, end)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(100));
        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(100));
        Assert.That(result.OEE, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_FullRuntime_NoStop_Availability100()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(15), 20)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(100));
        Assert.That(result.Performance, Is.GreaterThan(0));
    }

    [Test]
    public async Task CalculateOEE_PartialRuntime_StartThenStop_AvailabilityProportional()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.MachineStopped, start.AddMinutes(30))
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(50)); // 30 min / 60 min
    }

    [Test]
    public async Task CalculateOEE_MultipleRuntimeSegments_SummedCorrectly()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.MachineStopped, start.AddMinutes(10)),   // 10 min
            Evt(EventType.MachineStarted, start.AddMinutes(20)),
            Evt(EventType.MachineStopped, start.AddMinutes(30))    // 10 min
        };
        // Runtime: 10 + 10 = 20 min. Availability = 20/60 = 33.33%
        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(33.33m));
    }

    [Test]
    public async Task CalculateOEE_StillRunningAtPeriodEnd_IncludesTimeToEnd()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start.AddMinutes(30))  // 30 min to period end
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(50)); // 30 min / 60 min
    }

    [Test]
    public async Task CalculateOEE_OutOfOrderEvents_SortedByTimestamp()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStopped, start.AddMinutes(30)),
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(15), 5)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(50));
        Assert.That(result.Performance, Is.GreaterThan(0));
    }

    [Test]
    public async Task CalculateOEE_ProductionAndDefective_QualityCorrect()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(10), 10),
            Evt(EventType.DefectiveUnit, start.AddMinutes(15), 2),
            Evt(EventType.MachineStopped, end)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Quality, Is.EqualTo(80)); // 8 good / 10 total = 80%
    }

    [Test]
    public async Task CalculateOEE_NegativeQuantity_ClampedToZero()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(10), -5),
            Evt(EventType.MachineStopped, end)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_PerformanceFormula_IdealCycleTimeTimesProductionOverRuntime()
    {
        var machine = Machine(1, 60); // 60 sec per unit
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(10), 30), // 30 units in 1 hour
            Evt(EventType.MachineStopped, end)
        };
        // Ideal: 30 units * 60 sec = 1800 sec. Runtime = 3600. Performance = 1800/3600*100 = 50%
        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(100));
        Assert.That(result.Performance, Is.EqualTo(50));
        Assert.That(result.Quality, Is.EqualTo(100));
        Assert.That(result.OEE, Is.EqualTo(50));
    }

    [Test]
    public async Task CalculateOEE_PerformanceCappedAt100()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(1), 500),
            Evt(EventType.MachineStopped, end)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Performance, Is.LessThanOrEqualTo(100));
    }

    [Test]
    public async Task CalculateOEE_AllThreeFactors_OEEMultipliedCorrectly()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(15), 20),
            Evt(EventType.DefectiveUnit, start.AddMinutes(20), 5),
            Evt(EventType.MachineStopped, end)
        };
        // A=100, P=some value, Q=75 (15/20). OEE = A*P*Q/10000
        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(100));
        Assert.That(result.Quality, Is.EqualTo(75));
        Assert.That(result.OEE, Is.EqualTo((decimal)(100 * (double)result.Performance * 75 / 10000)));
    }

    [Test]
    public async Task CalculateOEE_MaintenanceEndsRuntimeSegment()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.Maintenance, start.AddMinutes(20)),
            Evt(EventType.MachineStarted, start.AddMinutes(40)),
            Evt(EventType.MachineStopped, end)
        };
        // Runtime: 20 min + 20 min = 40 min. Availability = 40/60 = 66.67 -> 66.67
        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.GreaterThanOrEqualTo(66));
        Assert.That(result.Availability, Is.LessThanOrEqualTo(67));
    }

    [Test]
    public async Task CalculateOEE_ZeroTotalTime_PeriodStartEqualsEnd_ReturnsAllZeros()
    {
        var machine = Machine(1, 10);
        var t = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent> { Evt(EventType.MachineStarted, t) };

        var result = await _service.CalculateOEEAsync(machine, events, t, t);

        Assert.That(result.Availability, Is.EqualTo(0));
        Assert.That(result.Performance, Is.EqualTo(0));
        Assert.That(result.Quality, Is.EqualTo(0));
        Assert.That(result.OEE, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_DefectiveExceedsProduction_GoodCountClampedToZero_QualityZero()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.ProductionCycle, start.AddMinutes(5), 5),
            Evt(EventType.DefectiveUnit, start.AddMinutes(10), 10),
            Evt(EventType.MachineStopped, end)
        };

        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Quality, Is.EqualTo(0));
    }

    [Test]
    public async Task CalculateOEE_IdleEndsRuntimeSegment()
    {
        var machine = Machine(1, 10);
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var events = new List<MachineEvent>
        {
            Evt(EventType.MachineStarted, start),
            Evt(EventType.Idle, start.AddMinutes(15)),
            Evt(EventType.MachineStarted, start.AddMinutes(30)),
            Evt(EventType.MachineStopped, end)
        };
        // 15 min + 30 min = 45 min. Availability = 75%
        var result = await _service.CalculateOEEAsync(machine, events, start, end);

        Assert.That(result.Availability, Is.EqualTo(75));
    }
}
