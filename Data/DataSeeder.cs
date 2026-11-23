using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;

namespace OEEMonitoringSystem.Data;

public static class DataSeeder
{
    private static readonly Random Rng = new(42);

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await db.Machines.AnyAsync())
            return;

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var admin = await userManager.FindByEmailAsync(RoleSeeder.GetAdminEmail(configuration));
        if (admin == null) return;

        var machines = CreateMachines();
        db.Machines.AddRange(machines);
        await db.SaveChangesAsync();

        foreach (var m in machines)
            db.UserMachines.Add(new UserMachine { UserId = admin.Id, MachineId = m.Id });
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        foreach (var m in machines)
        {
            var profile = GetMachineProfile(m.Name);
            var (events, oeeCalcs, alerts) = GenerateHistory(m.Id, m.TargetCycleTime, profile, now);

            db.MachineEvents.AddRange(events);
            db.OEECalculations.AddRange(oeeCalcs);
            db.Alerts.AddRange(alerts);
        }

        await db.SaveChangesAsync();
    }

    private static List<Machine> CreateMachines() => new()
    {
        new Machine
        {
            Name = "Assembly Line AL-100",
            Location = "Line A – Hall 1",
            TargetCycleTime = 8,
            IsActive = true,
            CurrentState = "Running",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        },
        new Machine
        {
            Name = "CNC Machine CT-200",
            Location = "Line A – Hall 1",
            TargetCycleTime = 12,
            IsActive = true,
            CurrentState = "Running",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        },
        new Machine
        {
            Name = "Conveyor Belt CB-50",
            Location = "Line B – Hall 2",
            TargetCycleTime = 6,
            IsActive = true,
            CurrentState = "Running",
            CreatedAt = DateTime.UtcNow.AddDays(-28)
        },
        new Machine
        {
            Name = "Press Machine PM-300",
            Location = "Line B – Hall 2",
            TargetCycleTime = 5,
            IsActive = true,
            CurrentState = "Stopped",
            CreatedAt = DateTime.UtcNow.AddDays(-28)
        },
        new Machine
        {
            Name = "Packaging Unit PU-75",
            Location = "Line C – Hall 3",
            TargetCycleTime = 10,
            IsActive = true,
            CurrentState = "Running",
            CreatedAt = DateTime.UtcNow.AddDays(-25)
        }
    };

    private record MachineProfile(
        double AvailMin, double AvailMax,
        double PerfMin, double PerfMax,
        double QualMin, double QualMax,
        double DefectRate,
        double MaintenanceChance,
        double IdleChance,
        bool Improving);

    private static MachineProfile GetMachineProfile(string name) => name switch
    {
        "Assembly Line AL-100"  => new(88, 98, 85, 97, 92, 99, 0.03, 0.05, 0.04, false),
        "CNC Machine CT-200"    => new(70, 82, 65, 78, 80, 90, 0.12, 0.12, 0.10, false),
        "Conveyor Belt CB-50"   => new(82, 94, 80, 93, 88, 97, 0.05, 0.07, 0.06, false),
        "Press Machine PM-300"  => new(40, 60, 35, 55, 50, 70, 0.25, 0.20, 0.18, false),
        "Packaging Unit PU-75"  => new(60, 92, 58, 90, 65, 95, 0.10, 0.10, 0.08, true),
        _ => new(75, 90, 70, 88, 80, 95, 0.08, 0.08, 0.06, false)
    };

    private static (List<MachineEvent>, List<OEECalculation>, List<Alert>) GenerateHistory(
        int machineId, double targetCycleTime, MachineProfile p, DateTime now)
    {
        var events = new List<MachineEvent>();
        var calcs = new List<OEECalculation>();
        var alerts = new List<Alert>();

        int totalDays = 14;

        for (int day = totalDays; day >= 0; day--)
        {
            double dayProgress = p.Improving ? (double)(totalDays - day) / totalDays : 0;

            for (int shift = 0; shift < 2; shift++)
            {
                var shiftStart = now.Date.AddDays(-day).AddHours(shift == 0 ? 6 : 14);
                var shiftEnd = shiftStart.AddHours(8);

                if (shiftEnd > now) shiftEnd = now;
                if (shiftStart >= now) continue;

                double shiftMinutes = (shiftEnd - shiftStart).TotalMinutes;
                if (shiftMinutes < 30) continue;

                double availTarget = Lerp(p.AvailMin, p.AvailMax, dayProgress);
                double perfTarget = Lerp(p.PerfMin, p.PerfMax, dayProgress);
                double qualTarget = Lerp(p.QualMin, p.QualMax, dayProgress);

                double avail = Clamp(availTarget + RngRange(-4, 4), 5, 100);
                double perf = Clamp(perfTarget + RngRange(-4, 4), 5, 100);
                double qual = Clamp(qualTarget + RngRange(-3, 3), 10, 100);

                double runtimeMinutes = shiftMinutes * avail / 100.0;
                double runtimeSeconds = runtimeMinutes * 60;
                int totalProduced = Math.Max(1, (int)(runtimeSeconds * perf / 100.0 / targetCycleTime));
                int defective = (int)(totalProduced * (1 - qual / 100.0));
                double goodRate = totalProduced > 0 ? (double)(totalProduced - defective) / totalProduced * 100 : 0;

                var cursor = shiftStart;

                events.Add(Evt(machineId, EventType.MachineStarted, cursor));
                cursor = cursor.AddMinutes(2);

                double runBlock = runtimeMinutes / 3.0;
                int prodPerBlock = totalProduced / 3;
                int defPerBlock = defective / 3;

                for (int block = 0; block < 3; block++)
                {
                    int qty = block < 2 ? prodPerBlock : totalProduced - 2 * prodPerBlock;
                    int def = block < 2 ? defPerBlock : defective - 2 * defPerBlock;

                    if (qty > 0)
                    {
                        events.Add(Evt(machineId, EventType.ProductionCycle, cursor, quantity: qty));
                        cursor = cursor.AddMinutes(runBlock * 0.8);
                    }

                    if (def > 0)
                    {
                        events.Add(Evt(machineId, EventType.DefectiveUnit, cursor, quantity: def));
                        cursor = cursor.AddMinutes(1);
                    }

                    if (block < 2)
                    {
                        bool doMaint = Rng.NextDouble() < p.MaintenanceChance;
                        bool doIdle = !doMaint && Rng.NextDouble() < p.IdleChance;

                        if (doMaint)
                        {
                            double downMin = RngRange(5, 25);
                            events.Add(Evt(machineId, EventType.MachineStopped, cursor));
                            cursor = cursor.AddMinutes(1);
                            events.Add(Evt(machineId, EventType.Maintenance, cursor, duration: downMin * 60));
                            cursor = cursor.AddMinutes(downMin);
                            events.Add(Evt(machineId, EventType.MachineStarted, cursor));
                            cursor = cursor.AddMinutes(1);
                        }
                        else if (doIdle)
                        {
                            double idleMin = RngRange(3, 15);
                            events.Add(Evt(machineId, EventType.Idle, cursor, duration: idleMin * 60));
                            cursor = cursor.AddMinutes(idleMin);
                            events.Add(Evt(machineId, EventType.MachineStarted, cursor));
                            cursor = cursor.AddMinutes(0.5);
                        }
                        else
                        {
                            cursor = cursor.AddMinutes(runBlock * 0.15);
                        }
                    }
                }

                if (cursor < shiftEnd)
                    events.Add(Evt(machineId, EventType.MachineStopped, shiftEnd.AddSeconds(-10)));

                decimal dAvail = Math.Round((decimal)avail, 2);
                decimal dPerf = Math.Round((decimal)perf, 2);
                decimal dQual = Math.Round((decimal)goodRate, 2);
                decimal dOee = Math.Round(dAvail * dPerf * dQual / 10000m, 2);

                calcs.Add(new OEECalculation
                {
                    MachineId = machineId,
                    Availability = dAvail,
                    Performance = dPerf,
                    Quality = dQual,
                    OEE = dOee,
                    CalculationTime = shiftEnd,
                    PeriodStart = shiftStart,
                    PeriodEnd = shiftEnd
                });

                if (dOee < 50)
                {
                    alerts.Add(new Alert
                    {
                        MachineId = machineId,
                        AlertType = "Low OEE",
                        Message = $"OEE dropped to {dOee}% during shift ({shiftStart:HH:mm}–{shiftEnd:HH:mm})",
                        Threshold = 50,
                        CurrentValue = dOee,
                        Timestamp = shiftEnd,
                        IsResolved = day > 1,
                        ResolvedAt = day > 1 ? shiftEnd.AddHours(2) : null
                    });
                }

                if (dAvail < 60)
                {
                    alerts.Add(new Alert
                    {
                        MachineId = machineId,
                        AlertType = "Low Availability",
                        Message = $"Availability at {dAvail}% – excessive downtime detected",
                        Threshold = 60,
                        CurrentValue = dAvail,
                        Timestamp = shiftEnd,
                        IsResolved = day > 2,
                        ResolvedAt = day > 2 ? shiftEnd.AddHours(1) : null
                    });
                }

                if (dQual < 75)
                {
                    alerts.Add(new Alert
                    {
                        MachineId = machineId,
                        AlertType = "Quality Drop",
                        Message = $"Quality fell to {dQual}% – defect rate above acceptable limit",
                        Threshold = 75,
                        CurrentValue = dQual,
                        Timestamp = shiftEnd,
                        IsResolved = day > 1,
                        ResolvedAt = day > 1 ? shiftEnd.AddHours(3) : null
                    });
                }
            }
        }

        return (events, calcs, alerts);
    }

    private static MachineEvent Evt(int machineId, EventType type, DateTime ts,
        int? quantity = null, double? duration = null) => new()
    {
        MachineId = machineId,
        EventType = type,
        Timestamp = ts,
        Quantity = quantity,
        Duration = duration
    };

    private static double RngRange(double min, double max) =>
        min + Rng.NextDouble() * (max - min);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
