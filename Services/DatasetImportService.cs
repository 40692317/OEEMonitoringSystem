using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class DatasetImportService : IDatasetImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatasetImportService> _logger;

        public DatasetImportService(IServiceProvider serviceProvider, ILogger<DatasetImportService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<OeeDatasetImportResult> ImportOEEDatasetAsync(Stream fileStream)
        {
            var result = new OeeDatasetImportResult();
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            using var workbook = new XLWorkbook(fileStream);
            var ws = workbook.Worksheets.First();
            var used = ws.RangeUsed();
            if (used == null) return result;

            int colCount = used.ColumnCount();
            int rowCount = used.RowCount();
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cell(1, c).GetString().Trim();
                if (!string.IsNullOrEmpty(h)) headers[h] = c;
            }

            int? colMachine = GetColumn(headers, "Printer", "Machine", "Machine Name", "MachineName", "MachineId");
            int? colDate = GetColumn(headers, "Date & Time", "Date", "Time", "Timestamp", "DateTime", "Date/Time");
            int? colEventType = GetColumn(headers, "EventType", "Event Type", "Type", "Event");
            int? colDuration = GetColumn(headers, "Duration", "Duration (s)", "DurationSeconds");
            int? colQuantity = GetColumn(headers, "Quantity", "Qty", "Count", "Actual Packs");
            int? colAvail = GetColumn(headers, "Availability %", "Availability");
            int? colPerf = GetColumn(headers, "Performance %", "Performance");
            int? colQuality = GetColumn(headers, "Quality %", "Quality");
            int? colOEE = GetColumn(headers, "OEE %", "OEE");

            bool isReportFormat = colAvail.HasValue && colPerf.HasValue && colQuality.HasValue && colOEE.HasValue && colMachine.HasValue && colDate.HasValue;
            if (isReportFormat)
                return await ImportReportFormatRowsAsync(context, ws, rowCount, colMachine!.Value, colDate.Value, colAvail!.Value, colPerf!.Value, colQuality!.Value, colOEE!.Value, result);

            if (!colMachine.HasValue || !colDate.HasValue)
            {
                result.Errors.Add("Required columns not found: need Printer/Machine and Date & Time (or event columns).");
                return result;
            }

            var machineByName = new Dictionary<string, Machine>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in await context.Machines.ToListAsync())
                machineByName[m.Name] = m;

            for (int r = 2; r <= rowCount + 1; r++)
            {
                try
                {
                    var nameCell = ws.Cell(r, colMachine.Value).GetString().Trim();
                    if (string.IsNullOrEmpty(nameCell)) continue;

                    if (!machineByName.TryGetValue(nameCell, out var machine))
                    {
                        machine = new Machine
                        {
                            Name = nameCell,
                            Location = "Imported",
                            TargetCycleTime = 1,
                            IsActive = true
                                               };
                        context.Machines.Add(machine);
                        await context.SaveChangesAsync();
                        machineByName[machine.Name] = machine;
                        result.MachinesCreated++;
                    }

                    DateTime timestamp;
                    var dateStr = ws.Cell(r, colDate.Value).GetString().Trim();
                    if (string.IsNullOrEmpty(dateStr))
                        dateStr = ws.Cell(r, colDate.Value).GetValue<object>()?.ToString() ?? "";
                    if (!DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out timestamp))
                        timestamp = DateTime.UtcNow;

                    if (timestamp.Kind != DateTimeKind.Utc)
                        timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

                    var eventType = EventType.MachineStarted;
                    if (colEventType.HasValue)
                    {
                        var typeStr = ws.Cell(r, colEventType.Value).GetString().Trim();
                        eventType = ParseEventType(typeStr);
                    }

                    double? duration = null;
                    if (colDuration.HasValue)
                    {
                        var durVal = ws.Cell(r, colDuration.Value).GetValue<object>();
                        if (durVal is double d) duration = d;
                        else if (durVal is int i) duration = i;
                        else if (double.TryParse(durVal?.ToString(), out var parsed)) duration = parsed;
                    }

                    int? quantity = null;
                    if (colQuantity.HasValue)
                    {
                        var qtyVal = ws.Cell(r, colQuantity.Value).GetValue<object>();
                        if (qtyVal is int i) quantity = i;
                        else if (qtyVal is double d) quantity = (int)d;
                        else if (int.TryParse(qtyVal?.ToString(), out var parsed)) quantity = parsed;
                    }

                    context.MachineEvents.Add(new MachineEvent
                    {
                        MachineId = machine.Id,
                        EventType = eventType,
                        Timestamp = timestamp,
                        Duration = duration,
                        Quantity = quantity
                    });
                    result.EventsImported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {r}: {ex.Message}");
                    _logger.LogWarning(ex, "Import OEE Dataset row {Row}", r);
                }
            }

            await context.SaveChangesAsync();
            return result;
        }

        public async Task<OeeReportImportResult> ImportOeeReportAsync(Stream fileStream)
        {
            var result = new OeeReportImportResult();
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            using var workbook = new XLWorkbook(fileStream);
            var ws = workbook.Worksheets.First();
            var used = ws.RangeUsed();
            if (used == null) return result;

            int colCount = used.ColumnCount();
            int rowCount = used.RowCount();
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cell(1, c).GetString().Trim();
                if (!string.IsNullOrEmpty(h)) headers[h] = c;
            }

            int? colMachine = GetColumn(headers, "Printer", "Machine", "Machine Name", "MachineName");
            int? colDate = GetColumn(headers, "Date & Time", "Date", "Time", "Timestamp", "DateTime", "Start", "Period Start", "StartDate");
            int? colEnd = GetColumn(headers, "PeriodEnd", "End", "End Date", "Period End", "EndDate");
            int? colAvail = GetColumn(headers, "Availability %", "Availability", "Avail");
            int? colPerf = GetColumn(headers, "Performance %", "Performance", "Perf");
            int? colQuality = GetColumn(headers, "Quality %", "Quality");
            int? colOEE = GetColumn(headers, "OEE %", "OEE");

            if (!colMachine.HasValue || !colAvail.HasValue || !colPerf.HasValue || !colQuality.HasValue || !colOEE.HasValue)
            {
                result.Errors.Add("Required columns not found: Printer (or Machine), Date & Time, Availability %, Performance %, Quality %, OEE %.");
                return result;
            }

            var machineByName = new Dictionary<string, Machine>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in await context.Machines.ToListAsync())
                machineByName[m.Name] = m;

            for (int r = 2; r <= rowCount + 1; r++)
            {
                try
                {
                    var nameCell = ws.Cell(r, colMachine.Value).GetString().Trim();
                    if (string.IsNullOrEmpty(nameCell)) continue;

                    if (!machineByName.TryGetValue(nameCell, out var machine))
                    {
                        machine = new Machine
                        {
                            Name = nameCell,
                            Location = "Imported",
                            TargetCycleTime = 1,
                            IsActive = true
                        };
                        context.Machines.Add(machine);
                        await context.SaveChangesAsync();
                        machineByName[machine.Name] = machine;
                        result.MachinesCreated++;
                    }

                    DateTime periodStart = DateTime.UtcNow.Date;
                    DateTime periodEnd = periodStart.AddHours(1);
                    if (colDate.HasValue)
                    {
                        var v = ws.Cell(r, colDate.Value).GetValue<object>();
                        if (v is DateTime dt) periodStart = dt;
                        else if (DateTime.TryParse(v?.ToString(), null, System.Globalization.DateTimeStyles.None, out var parsed)) periodStart = parsed;
                        if (periodStart.Kind != DateTimeKind.Utc) periodStart = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc);
                        periodEnd = periodStart.AddHours(1);
                    }
                    if (colEnd.HasValue)
                    {
                        var v = ws.Cell(r, colEnd.Value).GetValue<object>();
                        if (v is DateTime dt) periodEnd = dt;
                        else if (DateTime.TryParse(v?.ToString(), out var parsed)) periodEnd = parsed;
                        if (periodEnd.Kind != DateTimeKind.Utc) periodEnd = DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc);
                    }

                    decimal avail = GetDecimal(ws.Cell(r, colAvail!.Value));
                    decimal perf = GetDecimal(ws.Cell(r, colPerf!.Value));
                    decimal quality = GetDecimal(ws.Cell(r, colQuality!.Value));
                    decimal oee = colOEE.HasValue ? GetDecimal(ws.Cell(r, colOEE.Value)) : (avail * perf * quality / 10000);

                    context.OEECalculations.Add(new OEECalculation
                    {
                        MachineId = machine.Id,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        CalculationTime = DateTime.UtcNow,
                        Availability = avail,
                        Performance = perf,
                        Quality = quality,
                        OEE = oee
                    });
                    result.CalculationsImported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {r}: {ex.Message}");
                    _logger.LogWarning(ex, "Import OeeReport row {Row}", r);
                }
            }

            await context.SaveChangesAsync();
            return result;
        }

        public async Task<CombinedImportResult> ImportBothAsync(Stream oeeDatasetStream, Stream oeeReportStream)
        {
            var combined = new CombinedImportResult();
            var ds = await ImportOEEDatasetAsync(oeeDatasetStream);
            combined.MachinesCreated += ds.MachinesCreated;
            combined.EventsImported += ds.EventsImported;
            foreach (var e in ds.Errors) combined.Errors.Add("[OEE_Dataset] " + e);

            var rep = await ImportOeeReportAsync(oeeReportStream);
            combined.MachinesCreated += rep.MachinesCreated;
            combined.CalculationsImported += rep.CalculationsImported;
            foreach (var e in rep.Errors) combined.Errors.Add("[OeeReport] " + e);

            return combined;
        }

        private static async Task<OeeDatasetImportResult> ImportReportFormatRowsAsync(
            ApplicationDbContext context,
            IXLWorksheet ws,
            int rowCount,
            int colMachine,
            int colDate,
            int colAvail,
            int colPerf,
            int colQuality,
            int colOEE,
            OeeDatasetImportResult result)
        {
            var machineByName = new Dictionary<string, Machine>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in await context.Machines.ToListAsync())
                machineByName[m.Name] = m;

            for (int r = 2; r <= rowCount + 1; r++)
            {
                try
                {
                    var nameCell = ws.Cell(r, colMachine).GetString().Trim();
                    if (string.IsNullOrEmpty(nameCell)) continue;

                    if (!machineByName.TryGetValue(nameCell, out var machine))
                    {
                        machine = new Machine
                        {
                            Name = nameCell,
                            Location = "Imported",
                            TargetCycleTime = 1,
                            IsActive = true
                        };
                        context.Machines.Add(machine);
                        await context.SaveChangesAsync();
                        machineByName[machine.Name] = machine;
                        result.MachinesCreated++;
                    }

                    var v = ws.Cell(r, colDate).GetValue<object>();
                    DateTime periodStart = DateTime.UtcNow.Date;
                    if (v is DateTime dt) periodStart = dt;
                    else if (DateTime.TryParse(v?.ToString(), null, System.Globalization.DateTimeStyles.None, out var parsed)) periodStart = parsed;
                    if (periodStart.Kind != DateTimeKind.Utc) periodStart = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc);
                    var periodEnd = periodStart.AddHours(1);

                    decimal avail = GetDecimal(ws.Cell(r, colAvail));
                    decimal perf = GetDecimal(ws.Cell(r, colPerf));
                    decimal quality = GetDecimal(ws.Cell(r, colQuality));
                    decimal oee = GetDecimal(ws.Cell(r, colOEE));

                    context.OEECalculations.Add(new OEECalculation
                    {
                        MachineId = machine.Id,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        CalculationTime = DateTime.UtcNow,
                        Availability = avail,
                        Performance = perf,
                        Quality = quality,
                        OEE = oee
                    });
                    result.EventsImported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {r}: {ex.Message}");
                }
            }

            await context.SaveChangesAsync();
            return result;
        }

        private static int? GetColumn(Dictionary<string, int> headers, params string[] names)
        {
            foreach (var n in names)
                if (headers.TryGetValue(n, out var col)) return col;
            return null;
        }

        private static EventType ParseEventType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return EventType.MachineStarted;
            var v = value.Trim();
            if (int.TryParse(v, out var num) && num >= 1 && num <= 6) return (EventType)num;
            if (v.Equals("MachineStarted", StringComparison.OrdinalIgnoreCase)) return EventType.MachineStarted;
            if (v.Equals("MachineStopped", StringComparison.OrdinalIgnoreCase)) return EventType.MachineStopped;
            if (v.Equals("ProductionCycle", StringComparison.OrdinalIgnoreCase)) return EventType.ProductionCycle;
            if (v.Equals("DefectiveUnit", StringComparison.OrdinalIgnoreCase)) return EventType.DefectiveUnit;
            if (v.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)) return EventType.Maintenance;
            if (v.Equals("Idle", StringComparison.OrdinalIgnoreCase)) return EventType.Idle;
            return EventType.MachineStarted;
        }

        private static decimal GetDecimal(IXLCell cell)
        {
            var v = cell.GetValue<object>();
            if (v is double d) return (decimal)d;
            if (v is decimal dec) return dec;
            if (v is int i) return i;
            decimal.TryParse(v?.ToString(), out var parsed);
            return parsed;
        }
    }
}
