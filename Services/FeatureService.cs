using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models.ViewModel;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class FeatureService : IFeatureService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FeatureService> _logger;

        public FeatureService(IServiceProvider serviceProvider, ILogger<FeatureService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<MachineFeatureRow>> GetFeaturesForPeriodAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, int? topN = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var query = context.OEECalculations
                .Include(oc => oc.Machine)
                .Where(oc => oc.PeriodEnd >= startUtc && oc.PeriodStart <= endUtc);

            if (machineId.HasValue)
                query = query.Where(oc => oc.MachineId == machineId.Value);

            IQueryable<OEECalculation> ordered = topN.HasValue
                ? query.OrderByDescending(oc => oc.PeriodEnd).Take(topN.Value)
                : query.OrderBy(oc => oc.MachineId).ThenBy(oc => oc.PeriodStart);

            var calculations = await ordered.ToListAsync();

            var result = new List<MachineFeatureRow>();
            foreach (var calc in calculations)
            {
                var events = await context.MachineEvents
                    .Where(e => e.MachineId == calc.MachineId
                        && e.Timestamp >= calc.PeriodStart
                        && e.Timestamp <= calc.PeriodEnd)
                    .ToListAsync();

                int stoppageCount = events.Count(e =>
                    e.EventType == EventType.MachineStopped
                    || e.EventType == EventType.Maintenance
                    || e.EventType == EventType.Idle);
                double stoppageDuration = events
                    .Where(e => e.EventType == EventType.MachineStopped || e.EventType == EventType.Maintenance || e.EventType == EventType.Idle)
                    .Sum(e => e.Duration ?? 0);
                int productionQty = events.Where(e => e.EventType == EventType.ProductionCycle).Sum(e => e.Quantity ?? 0);
                int defectQty = events.Where(e => e.EventType == EventType.DefectiveUnit).Sum(e => e.Quantity ?? 0);

                result.Add(new MachineFeatureRow
                {
                    MachineId = calc.MachineId,
                    MachineName = calc.Machine?.Name ?? "",
                    PeriodStart = calc.PeriodStart,
                    PeriodEnd = calc.PeriodEnd,
                    TargetCycleTime = calc.Machine?.TargetCycleTime ?? 0,
                    Availability = calc.Availability,
                    Performance = calc.Performance,
                    Quality = calc.Quality,
                    OEE = calc.OEE,
                    StoppageCount = stoppageCount,
                    StoppageDurationTotalSeconds = stoppageDuration,
                    ProductionQuantity = productionQty,
                    DefectQuantity = defectQty
                });
            }

            return result;
        }

        public Task<byte[]> ExportFeaturesToCsvAsync(List<MachineFeatureRow> rows)
        {
            var csv = new StringBuilder();
            csv.AppendLine("MachineId,MachineName,PeriodStart,PeriodEnd,TargetCycleTime,Availability,Performance,Quality,OEE,StoppageCount,StoppageDurationTotalSeconds,ProductionQuantity,DefectQuantity");
            foreach (var r in rows)
            {
                csv.AppendLine($"{r.MachineId},\"{r.MachineName}\",{r.PeriodStart:O},{r.PeriodEnd:O},{r.TargetCycleTime},{r.Availability},{r.Performance},{r.Quality},{r.OEE},{r.StoppageCount},{r.StoppageDurationTotalSeconds},{r.ProductionQuantity},{r.DefectQuantity}");
            }
            return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
        }

        public Task<byte[]> ExportFeaturesToExcelAsync(List<MachineFeatureRow> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Features");
            ws.Cell(1, 1).Value = "MachineId";
            ws.Cell(1, 2).Value = "MachineName";
            ws.Cell(1, 3).Value = "PeriodStart";
            ws.Cell(1, 4).Value = "PeriodEnd";
            ws.Cell(1, 5).Value = "TargetCycleTime";
            ws.Cell(1, 6).Value = "Availability";
            ws.Cell(1, 7).Value = "Performance";
            ws.Cell(1, 8).Value = "Quality";
            ws.Cell(1, 9).Value = "OEE";
            ws.Cell(1, 10).Value = "StoppageCount";
            ws.Cell(1, 11).Value = "StoppageDurationTotalSeconds";
            ws.Cell(1, 12).Value = "ProductionQuantity";
            ws.Cell(1, 13).Value = "DefectQuantity";
            int row = 2;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.MachineId;
                ws.Cell(row, 2).Value = r.MachineName;
                ws.Cell(row, 3).Value = r.PeriodStart;
                ws.Cell(row, 4).Value = r.PeriodEnd;
                ws.Cell(row, 5).Value = r.TargetCycleTime;
                ws.Cell(row, 6).Value = (double)r.Availability;
                ws.Cell(row, 7).Value = (double)r.Performance;
                ws.Cell(row, 8).Value = (double)r.Quality;
                ws.Cell(row, 9).Value = (double)r.OEE;
                ws.Cell(row, 10).Value = r.StoppageCount;
                ws.Cell(row, 11).Value = r.StoppageDurationTotalSeconds;
                ws.Cell(row, 12).Value = r.ProductionQuantity;
                ws.Cell(row, 13).Value = r.DefectQuantity;
                row++;
            }
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
        }
    }
}
