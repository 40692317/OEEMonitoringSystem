
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Data;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.ViewModels;
using OEEMonitoringSystem.Services.Interfaces;
using System.Text;

namespace OEEMonitoringSystem.Services
{
    public class ReportService : IReportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITimezoneService _timezoneService;
        private readonly ILogger<ReportService> _logger;

        public ReportService(
            IServiceProvider serviceProvider,
            ITimezoneService timezoneService,
            ILogger<ReportService> logger)
        {
            _serviceProvider = serviceProvider;
            _timezoneService = timezoneService;
            _logger = logger;
        }

        public async Task<List<OEECalculation>> GetOEECalculationsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Ensure filters are UTC because DB stores UTC
                startDate = _timezoneService.ConvertUserTimeToUtc(startDate);
                endDate = _timezoneService.ConvertUserTimeToUtc(endDate);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var calculations = await context.OEECalculations
                    .Include(oc => oc.Machine)
                    .Where(oc => oc.PeriodEnd >= startDate && oc.PeriodStart <= endDate) // ✅ overlap filter
                    .OrderBy(oc => oc.Machine.Name)
                    .ThenBy(oc => oc.PeriodStart)
                    .ToListAsync();

                // Convert back to local for display
                foreach (var calc in calculations)
                {
                    //calc.CalculationTime = _timezoneService.ConvertUtcToUserTime(calc.CalculationTime);
                    //calc.PeriodStart = _timezoneService.ConvertUtcToUserTime(calc.PeriodStart);
                    //calc.PeriodEnd = _timezoneService.ConvertUtcToUserTime(calc.PeriodEnd);
                    var calculation = calc.DisplayCalculationTime;
                    var periodStart = calc.DisplayPeriodStart;
                    var periodEnd = calc.DisplayPeriodEnd;


                }


                return calculations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OEE calculations from {StartDate} to {EndDate}",
                    startDate, endDate);
                throw;
            }
        }

        public async Task<List<OEECalculation>> GetOEECalculationsForReportAsync(DateTime startDate, DateTime endDate, List<int>? machineIds)
        {
            try
            {
                startDate = _timezoneService.ConvertUserTimeToUtc(startDate);
                endDate = _timezoneService.ConvertUserTimeToUtc(endDate);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var query = context.OEECalculations
                    .Include(oc => oc.Machine)
                    .Where(oc => oc.PeriodEnd >= startDate && oc.PeriodStart <= endDate);

                // ✅ Apply machine filter
                if (machineIds != null && machineIds.Any())
                {
                    query = query.Where(oc => machineIds.Contains(oc.MachineId));
                }

                var calculations = await query
                    .OrderBy(oc => oc.Machine.Name)
                    .ThenBy(oc => oc.PeriodStart)
                    .ToListAsync();

                foreach (var calc in calculations)
                {
                    //calc.CalculationTime = _timezoneService.ConvertUtcToUserTime(calc.CalculationTime);
                    //calc.PeriodStart = _timezoneService.ConvertUtcToUserTime(calc.PeriodStart);
                    //calc.PeriodEnd = _timezoneService.ConvertUtcToUserTime(calc.PeriodEnd);
                    var calculation = calc.DisplayCalculationTime;
                    var periodStart = calc.DisplayPeriodStart;
                    var periodEnd = calc.DisplayPeriodEnd;
                }

                return calculations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OEE calculations");
                throw;
            }
        }

        public async Task<byte[]> ExportToExcelAsync(List<OEECalculation> calculations)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("OEE Report");

                // Headers
                worksheet.Cell(1, 1).Value = "Machine Name";
                worksheet.Cell(1, 2).Value = "Period Start";
                worksheet.Cell(1, 3).Value = "Period End";
                worksheet.Cell(1, 4).Value = "Availability (%)";
                worksheet.Cell(1, 5).Value = "Performance (%)";
                worksheet.Cell(1, 6).Value = "Quality (%)";
                worksheet.Cell(1, 7).Value = "OEE (%)";

                // Data with timezone conversion
                for (int i = 0; i < calculations.Count; i++)
                {
                    var calc = calculations[i];
                    worksheet.Cell(i + 2, 1).Value = calc.Machine?.Name ?? "Unknown";

                    // Convert timestamps to local time for export
                    //var periodStartLocal = _timezoneService.FormatDateTimeForDisplay(calc.PeriodStart);
                    //var periodEndLocal = _timezoneService.FormatDateTimeForDisplay(calc.PeriodEnd);
                    var periodStartLocal = calc.DisplayPeriodStart;
                    var periodEndLocal = calc.DisplayPeriodEnd;

                    worksheet.Cell(i + 2, 2).Value = periodStartLocal;
                    worksheet.Cell(i + 2, 3).Value = periodEndLocal;

                    worksheet.Cell(i + 2, 4).Value = calc.Availability;
                    worksheet.Cell(i + 2, 5).Value = calc.Performance;
                    worksheet.Cell(i + 2, 6).Value = calc.Quality;
                    worksheet.Cell(i + 2, 7).Value = calc.OEE;
                }

                // Format headers
                var headerRange = worksheet.Range(1, 1, 1, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to Excel");
                throw;
            }
        }

        public async Task<byte[]> ExportToCsvAsync(List<OEECalculation> calculations)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Machine Name,Period Start,Period End,Availability (%),Performance (%),Quality (%),OEE (%)");

                foreach (var calc in calculations)
                {
                    // Convert timestamps to local time for export
                    //var periodStartLocal = _timezoneService.FormatDateTimeForDisplay(calc.PeriodStart);
                    //var periodEndLocal = _timezoneService.FormatDateTimeForDisplay(calc.PeriodEnd);
                    var periodStartLocal = calc.DisplayPeriodStart;
                    var periodEndLocal = calc.DisplayPeriodEnd;

                    csv.AppendLine($"{calc.Machine?.Name ?? "Unknown"}," +
                                  $"{periodStartLocal}," +
                                  $"{periodEndLocal}," +
                                  $"{calc.Availability}," +
                                  $"{calc.Performance}," +
                                  $"{calc.Quality}," +
                                  $"{calc.OEE}");
                }

                return Encoding.UTF8.GetBytes(csv.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to CSV");
                throw;
            }
        }

        public async Task<List<OEECalculation>> GetHistoricalTrendsAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                if (startDate.Kind == DateTimeKind.Unspecified)
                    startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                if (endDate.Kind == DateTimeKind.Unspecified)
                    endDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                var calculations = await context.OEECalculations
                    .Where(oc => oc.MachineId == machineId &&
                                oc.PeriodStart >= startDate &&
                                oc.PeriodEnd <= endDate)
                    .OrderBy(oc => oc.PeriodStart)
                    .ToListAsync();

                return calculations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical trends for machine {MachineId}", machineId);
                throw;
            }
        }
        public async Task<List<WeeklyOEEReportViewModel>> GetWeeklyOEEReportAsync(DateTime start, DateTime end, List<int> machineIds)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (start.Kind == DateTimeKind.Unspecified)
                start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            if (end.Kind == DateTimeKind.Unspecified)
                end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            var query = context.OEECalculations
                .Include(c => c.Machine)
                .Where(c => c.PeriodStart >= start && c.PeriodEnd <= end);

            if (machineIds.Any())
                query = query.Where(c => machineIds.Contains(c.MachineId));

            var data = await query.ToListAsync();

            var grouped = data
                .GroupBy(c => new
                {
                    c.MachineId,
                    WeekStart = c.PeriodStart.Date.AddDays(-(int)c.PeriodStart.DayOfWeek + (c.PeriodStart.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)),
                    WeekEnd = c.PeriodStart.Date.AddDays(7 - (int)c.PeriodStart.DayOfWeek)
                })
                .Select(g => new WeeklyOEEReportViewModel
                {
                    MachineId = g.Key.MachineId,
                    MachineName = g.First().Machine?.Name ?? "Unknown",
                    WeekStart = g.Key.WeekStart,
                    WeekEnd = g.Key.WeekEnd,
                    Availability = (double)g.Average(x => x.Availability),
                    Performance = (double)g.Average(x => x.Performance),
                    Quality = (double)g.Average(x => x.Quality),
                    OEE = (double)g.Average(x => x.OEE)
                })
                .OrderBy(r => r.WeekStart)
                .ToList();

            return grouped;
        }
        public async Task<byte[]> ExportWeeklyToCsvAsync(List<WeeklyOEEReportViewModel> weeklyData)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Machine Name,Week Start,Week End,Availability (%),Performance (%),Quality (%),OEE (%)");

                foreach (var calc in weeklyData)
                {
                    csv.AppendLine($"{calc.MachineName}," +
                                   $"{calc.WeekStart:yyyy-MM-dd}," +
                                   $"{calc.WeekEnd:yyyy-MM-dd}," +
                                   $"{calc.Availability:F2}," +
                                   $"{calc.Performance:F2}," +
                                   $"{calc.Quality:F2}," +
                                   $"{calc.OEE:F2}");
                }

                return Encoding.UTF8.GetBytes(csv.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting weekly report to CSV");
                throw;
            }
        }

        public async Task<byte[]> ExportWeeklyToExcelAsync(List<WeeklyOEEReportViewModel> weeklyData)
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Weekly OEE Report");

                // Header
                worksheet.Cell(1, 1).Value = "Machine Name";
                worksheet.Cell(1, 2).Value = "Week Start";
                worksheet.Cell(1, 3).Value = "Week End";
                worksheet.Cell(1, 4).Value = "Availability (%)";
                worksheet.Cell(1, 5).Value = "Performance (%)";
                worksheet.Cell(1, 6).Value = "Quality (%)";
                worksheet.Cell(1, 7).Value = "OEE (%)";

                int row = 2;
                foreach (var calc in weeklyData)
                {
                    worksheet.Cell(row, 1).Value = calc.MachineName;
                    worksheet.Cell(row, 2).Value = calc.WeekStart.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 3).Value = calc.WeekEnd.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 4).Value = calc.Availability;
                    worksheet.Cell(row, 5).Value = calc.Performance;
                    worksheet.Cell(row, 6).Value = calc.Quality;
                    worksheet.Cell(row, 7).Value = calc.OEE;
                    row++;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting weekly report to Excel");
                throw;
            }
        }



    }
}