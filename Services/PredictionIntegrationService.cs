using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models.ML;
using OEEMonitoringSystem.Models.ViewModel;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class PredictionIntegrationService : IPredictionIntegrationService
    {
        private readonly IFeatureService _featureService;
        private readonly IRegressionModelService _regressionModelService;
        private readonly IClassificationModelService _classificationModelService;

        public PredictionIntegrationService(
            IFeatureService featureService,
            IRegressionModelService regressionModelService,
            IClassificationModelService classificationModelService)
        {
            _featureService = featureService;
            _regressionModelService = regressionModelService;
            _classificationModelService = classificationModelService;
        }

        public async Task<PredictionResult> GetPredictionAsync(int machineId, DateTime periodStartUtc, DateTime periodEndUtc)
        {
            var rows = await _featureService.GetFeaturesForPeriodAsync(periodStartUtc, periodEndUtc, machineId, topN: 1);
            var row = rows.FirstOrDefault();
            if (row == null)
            {
                return new PredictionResult
                {
                    State = MachineState.Normal,
                    PredictedOEE = null
                };
            }

            var csvRow = ToOeeCsvRow(row);
            var state = _classificationModelService.PredictFromCsv(csvRow)
                ?? _classificationModelService.GetStateFromRules(row.Availability, row.Performance, row.Quality, row.OEE);
            var predictedOEE = _regressionModelService.PredictFromCsv(csvRow);

            return new PredictionResult
            {
                State = state,
                PredictedOEE = predictedOEE
            };
        }

        private static OeeCsvRow ToOeeCsvRow(MachineFeatureRow row)
        {
            var avail = (float)row.Availability;
            var perf = (float)row.Performance;
            var actual = avail / 100f;
            return new OeeCsvRow
            {
                Availability = avail,
                Performance = perf,
                Actual = actual,
                Packs = row.ProductionQuantity,
                Stop = 0f,
                Start = 0f,
                Processing = 0f,
                Printing = actual,
                Idle = Math.Max(0f, 1f - actual),
                OEE = (float)row.OEE,
                State = "Normal"
            };
        }
    }
}
