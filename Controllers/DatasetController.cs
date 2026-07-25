using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OEEMonitoringSystem.Models;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DatasetController : Controller
    {
        private readonly IFeatureService _featureService;
        private readonly ITimezoneService _timezoneService;
        private readonly IMachineService _machineService;
        private readonly IDatasetImportService _importService;
        private readonly IRegressionModelService _regressionModelService;
        private readonly IClassificationModelService _classificationModelService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public DatasetController(
            IFeatureService featureService,
            ITimezoneService timezoneService,
            IMachineService machineService,
            IDatasetImportService importService,
            IRegressionModelService regressionModelService,
            IClassificationModelService classificationModelService,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _featureService = featureService;
            _timezoneService = timezoneService;
            _machineService = machineService;
            _importService = importService;
            _regressionModelService = regressionModelService;
            _classificationModelService = classificationModelService;
            _configuration = configuration;
            _env = env;
        }


        public IActionResult Index()
        {
            return RedirectToAction(nameof(ML));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetFeatures(DateTime startDate, DateTime endDate, int? machineId)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.MachineId = machineId;
            return View("Index", rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCsv(DateTime startDate, DateTime endDate, int? machineId)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            var bytes = await _featureService.ExportFeaturesToCsvAsync(rows);
            return File(bytes, "text/csv", "OEE_Features.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel(DateTime startDate, DateTime endDate, int? machineId)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            var bytes = await _featureService.ExportFeaturesToExcelAsync(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OEE_Features.xlsx");
        }

        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportOEEDataset(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select OEE_Dataset.xlsx file.";
                return RedirectToAction(nameof(Import));
            }
            using var stream = file.OpenReadStream();
            var result = await _importService.ImportOEEDatasetAsync(stream);
            TempData["Success"] = $"OEE Dataset: {result.MachinesCreated} machine(s) created, {result.EventsImported} events imported.";
            if (result.Errors.Any())
                TempData["Warning"] = string.Join("; ", result.Errors.Take(5));
            return RedirectToAction(nameof(Import));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportOeeReport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select OeeReport .xlsx file.";
                return RedirectToAction(nameof(Import));
            }
            using var stream = file.OpenReadStream();
            var result = await _importService.ImportOeeReportAsync(stream);
            TempData["Success"] = $"OeeReport: {result.MachinesCreated} machine(s) created, {result.CalculationsImported} OEE calculations imported.";
            if (result.Errors.Any())
                TempData["Warning"] = string.Join("; ", result.Errors.Take(5));
            return RedirectToAction(nameof(Import));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportBoth(IFormFile oeeDataset, IFormFile oeeReport)
        {
            if (oeeDataset == null || oeeDataset.Length == 0 || oeeReport == null || oeeReport.Length == 0)
            {
                TempData["Error"] = "Please select both OEE_Dataset.xlsx and OeeReport.xlsx.";
                return RedirectToAction(nameof(Import));
            }
            using var stream1 = oeeDataset.OpenReadStream();
            using var stream2 = oeeReport.OpenReadStream();
            var result = await _importService.ImportBothAsync(stream1, stream2);
            TempData["Success"] = $"Combined import: {result.MachinesCreated} machine(s), {result.EventsImported} events, {result.CalculationsImported} OEE calculations. Use this data for ML training.";
            if (result.Errors.Any())
                TempData["Warning"] = string.Join("; ", result.Errors.Take(5));
            return RedirectToAction(nameof(Import));
        }

        [HttpGet]
        public async Task<IActionResult> ML()
        {
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrainAllModels()
        {
            var testFraction = _configuration.GetValue<double>("ML:TestFraction", 0.2);
            testFraction = Math.Clamp(testFraction, 0.1, 0.5);

            var csvRel = _configuration["ML:OeeDatasetCsv"] ?? "ML/OEE_dataset.csv";
            var csvPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, csvRel));

            if (!System.IO.File.Exists(csvPath))
            {
                TempData["Error"] = $"OEE dataset CSV not found at: {csvPath}. Please run ML/prepare_dataset.py first.";
                return RedirectToAction(nameof(ML));
            }

            var regGbt = await _regressionModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.Default);
            var regForest = await _regressionModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.FastForest);
            var regSdca = await _regressionModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.Sdca);

            var clsGbt = await _classificationModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.Default);
            var clsForest = await _classificationModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.FastForest);
            var clsSdca = await _classificationModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.Sdca);

            TempData["RegDefault"] = FormatRegression(regGbt);
            TempData["RegForest"] = FormatRegression(regForest);
            TempData["RegSdca"] = FormatRegression(regSdca);
            TempData["ClsDefault"] = FormatClassification(clsGbt);
            TempData["ClsForest"] = FormatClassification(clsForest);
            TempData["ClsSdca"] = FormatClassification(clsSdca);

            TempData["CmGbt"] = FormatConfusionMatrix(clsGbt);
            TempData["CmForest"] = FormatConfusionMatrix(clsForest);
            TempData["CmSdca"] = FormatConfusionMatrix(clsSdca);

            var regResults = new[] { (regGbt, MLAlgorithm.Default), (regForest, MLAlgorithm.FastForest), (regSdca, MLAlgorithm.Sdca) };
            var bestReg = regResults.OrderByDescending(r => r.Item1.RSquared).First();
            await _regressionModelService.TrainFromCsvAsync(csvPath, testFraction, bestReg.Item2);

            var clsResults = new[] { (clsGbt, MLAlgorithm.Default), (clsForest, MLAlgorithm.FastForest), (clsSdca, MLAlgorithm.Sdca) };
            var bestCls = clsResults.OrderByDescending(r => r.Item1.MacroAccuracy).First();

            // Lock final active models for live system:
            // - Regression: best by R² (typically FastTree)
            // - Classification: always Random Forest (for stable, explainable alerts)
            await _classificationModelService.TrainFromCsvAsync(csvPath, testFraction, MLAlgorithm.FastForest);

            TempData["Success"] = $"Comparison complete. Regression winner: {bestReg.Item1.AlgorithmName} (saved). Classification winner by metrics: {bestCls.Item1.AlgorithmName}. Active classification model: Random Forest.";

            return RedirectToAction(nameof(ML));
        }

        private static string FormatRegression(RegressionMetricsResult m)
            => $"{m.AlgorithmName}|{m.RMSE:F4}|{m.MAE:F4}|{m.RSquared:F4}|{m.TrainingRows}|{m.TestRows}";

        private static string FormatClassification(ClassificationMetricsResult m)
            => $"{m.AlgorithmName}|{m.MacroAccuracy:F4}|{m.LogLoss:F4}|{m.TrainingRows}|{m.TestRows}";

        private static string FormatConfusionMatrix(ClassificationMetricsResult m)
            => JsonSerializer.Serialize(new { labels = m.ClassLabels, counts = m.ConfusionMatrixCounts });

        [HttpGet]
        public async Task<IActionResult> Regression()
        {
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrainRegression(DateTime startDate, DateTime endDate, int? machineId, double testFraction = 0.2)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            testFraction = Math.Clamp(testFraction, 0.1, 0.5);
            var metrics = await _regressionModelService.TrainAsync(startUtc, endUtc, machineId, testFraction);
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            ViewBag.TrainMetrics = metrics;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.MachineId = machineId;
            TempData["Success"] = $"Model trained. RMSE={metrics.RMSE:F2}, MAE={metrics.MAE:F2}, R²={metrics.RSquared:F3}. Training rows={metrics.TrainingRows}, Test rows={metrics.TestRows}.";
            return View("Regression", metrics);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateRegression(DateTime startDate, DateTime endDate, int? machineId)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            var metrics = await _regressionModelService.EvaluateAsync(startUtc, endUtc, machineId);
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            if (metrics == null)
            {
                TempData["Warning"] = "No saved model found. Train a model first.";
                return View("Regression");
            }
            TempData["Success"] = $"Evaluation: RMSE={metrics.RMSE:F2}, MAE={metrics.MAE:F2}, R²={metrics.RSquared:F3}.";
            return View("Regression", metrics);
        }

        [HttpGet]
        public async Task<IActionResult> Classification()
        {
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrainClassification(DateTime startDate, DateTime endDate, int? machineId, double testFraction = 0.2)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            testFraction = Math.Clamp(testFraction, 0.1, 0.5);
            var metrics = await _classificationModelService.TrainAsync(startUtc, endUtc, machineId, testFraction);
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.MachineId = machineId;
            TempData["Success"] = $"Classification model trained. Macro Accuracy={metrics.MacroAccuracy:F3}, LogLoss={metrics.LogLoss:F3}. Training rows={metrics.TrainingRows}, Test rows={metrics.TestRows}.";
            return View("Classification", metrics);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateClassification(DateTime startDate, DateTime endDate, int? machineId)
        {
            var startUtc = _timezoneService.ConvertUserTimeToUtc(startDate);
            var endUtc = _timezoneService.ConvertUserTimeToUtc(endDate);
            var metrics = await _classificationModelService.EvaluateAsync(startUtc, endUtc, machineId);
            ViewBag.Machines = await _machineService.GetAllMachinesAsync();
            if (metrics == null)
            {
                TempData["Warning"] = "No saved classification model. Train a model first.";
                return View("Classification");
            }
            TempData["Success"] = $"Evaluation: Macro Accuracy={metrics.MacroAccuracy:F3}, LogLoss={metrics.LogLoss:F3}.";
            return View("Classification", metrics);
        }
    }
}
