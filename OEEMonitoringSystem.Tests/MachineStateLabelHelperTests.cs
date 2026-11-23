using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services;
using NUnit.Framework;

namespace OEEMonitoringSystem.Tests;

[TestFixture]
public class MachineStateLabelHelperTests
{
    [Test]
    [TestCase(80, 85, 90, 70)]
    [TestCase(81, 86, 91, 71)]
    public void GetStateLabel_AboveThresholds_ReturnsNormal(decimal a, decimal p, decimal q, decimal oee)
    {
        var label = MachineStateLabelHelper.GetStateLabel(a, p, q, oee);
        Assert.That(label, Is.EqualTo("Normal"));
    }

    [Test]
    [TestCase(79, 85, 90, 70)]
    [TestCase(80, 84, 90, 70)]
    [TestCase(80, 85, 89, 70)]
    [TestCase(80, 85, 90, 69)]
    public void GetStateLabel_BelowOneWarningThreshold_ReturnsWarning(decimal a, decimal p, decimal q, decimal oee)
    {
        var label = MachineStateLabelHelper.GetStateLabel(a, p, q, oee);
        Assert.That(label, Is.EqualTo("Warning"));
    }

    [Test]
    [TestCase(49, 85, 90, 70)]
    [TestCase(80, 49, 90, 70)]
    [TestCase(80, 85, 49, 70)]
    [TestCase(80, 85, 90, 49)]
    [TestCase(0, 0, 0, 0)]
    public void GetStateLabel_BelowCriticalThreshold_ReturnsCritical(decimal a, decimal p, decimal q, decimal oee)
    {
        var label = MachineStateLabelHelper.GetStateLabel(a, p, q, oee);
        Assert.That(label, Is.EqualTo("Critical"));
    }

    [Test]
    public void GetState_ReturnsCorrectEnum()
    {
        Assert.That(MachineStateLabelHelper.GetState(80, 85, 90, 70), Is.EqualTo(MachineState.Normal));
        Assert.That(MachineStateLabelHelper.GetState(79, 85, 90, 70), Is.EqualTo(MachineState.Warning));
        Assert.That(MachineStateLabelHelper.GetState(30, 85, 90, 70), Is.EqualTo(MachineState.Critical));
    }

    [Test]
    public void Constants_MatchExpectedThresholds()
    {
        Assert.That(MachineStateLabelHelper.CriticalThreshold, Is.EqualTo(50));
        Assert.That(MachineStateLabelHelper.WarningAvailability, Is.EqualTo(80));
        Assert.That(MachineStateLabelHelper.WarningPerformance, Is.EqualTo(85));
        Assert.That(MachineStateLabelHelper.WarningQuality, Is.EqualTo(90));
        Assert.That(MachineStateLabelHelper.WarningOEE, Is.EqualTo(70));
    }
}
