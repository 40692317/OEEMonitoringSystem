using Microsoft.AspNetCore.Hosting;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.FastTree;
using OEEMonitoringSystem.Models.Enums;
using OEEMonitoringSystem.Models.ML;
using OEEMonitoringSystem.Models.ViewModel;
using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class RegressionModelService : IRegressionModelService
    {
        private readonly IFeatureService _featureService;
        private readonly ILogger<RegressionModelService> _logger;
        private readonly string _modelPath;
        private MLContext? _mlContext;
        private ITransformer? _trainedModel;

        public RegressionModelService(
            IFeatureService featureService,
            ILogger<RegressionModelService> logger,
            IWebHostEnvironment env)
        {
            _featureService = featureService;
            _logger = logger;
            _modelPath = Path.Combine(env.ContentRootPath, "MLModels", "OeeRegression.zip");
        }

        private static string AlgorithmLabel(MLAlgorithm alg) => alg switch
        {
            MLAlgorithm.FastForest => "Random Forest",
            MLAlgorithm.Sdca => "SDCA",
            _ => "FastTree"
        };

        public Task<RegressionMetricsResult> TrainFromCsvAsync(string csvPath, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default)
        {
            if (!File.Exists(csvPath))
            {
                _logger.LogWarning("CSV not found: {Path}", csvPath);
                return Task.FromResult(new RegressionMetricsResult());
            }

            var mlContext = new MLContext(seed: 42);
            var rawData = mlContext.Data.LoadFromTextFile<OeeCsvRow>(
                csvPath, hasHeader: true, separatorChar: ',');

            var rowCount = rawData.GetRowCount();
            _logger.LogInformation("CSV loaded: {Rows} rows for regression training ({Algo})", rowCount, algorithm);

            var featurePipeline = mlContext.Transforms
                .CopyColumns("Label", nameof(OeeCsvRow.OEE))
                .Append(mlContext.Transforms.Concatenate("Features",
                    nameof(OeeCsvRow.Availability),
                    nameof(OeeCsvRow.Performance),
                    nameof(OeeCsvRow.Actual),
                    nameof(OeeCsvRow.Packs),
                    nameof(OeeCsvRow.Stop),
                    nameof(OeeCsvRow.Start),
                    nameof(OeeCsvRow.Processing),
                    nameof(OeeCsvRow.Printing),
                    nameof(OeeCsvRow.Idle)))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"));

            IEstimator<ITransformer> pipeline = algorithm switch
            {
                MLAlgorithm.FastForest => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.FastForest(
                    labelColumnName: "Label", featureColumnName: "Features",
                    numberOfLeaves: 20, numberOfTrees: 100, minimumExampleCountPerLeaf: 10)),
                MLAlgorithm.Sdca => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "Label", featureColumnName: "Features",
                    maximumNumberOfIterations: 200)),
                _ => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "Label", featureColumnName: "Features",
                    numberOfLeaves: 20, numberOfTrees: 100, minimumExampleCountPerLeaf: 10))
            };

            var split = mlContext.Data.TrainTestSplit(rawData, testFraction: (float)testFraction, seed: 42);
            var totalRows = (int)(rawData.GetRowCount() ?? 0);
            if (totalRows == 0)
                totalRows = File.ReadLines(csvPath).Count() - 1;
            var testRows = (int)Math.Round(totalRows * testFraction);
            var trainRows = totalRows - testRows;

            _trainedModel = pipeline.Fit(split.TrainSet);
            _mlContext = mlContext;

            Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
            mlContext.Model.Save(_trainedModel, rawData.Schema, _modelPath);
            _logger.LogInformation("Regression model ({Algo}) saved to {Path}", algorithm, _modelPath);

            var predictions = _trainedModel.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            return Task.FromResult(new RegressionMetricsResult
            {
                RMSE = metrics.RootMeanSquaredError,
                MAE = metrics.MeanAbsoluteError,
                RSquared = metrics.RSquared,
                TrainingRows = trainRows,
                TestRows = testRows,
                AlgorithmName = AlgorithmLabel(algorithm)
            });
        }

        public async Task<RegressionMetricsResult> TrainAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default)
        {
            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            if (rows.Count < 10)
            {
                _logger.LogWarning("Insufficient data for training: {Count} rows. Need at least 10.", rows.Count);
                return new RegressionMetricsResult { TrainingRows = rows.Count, TestRows = 0 };
            }

            var data = rows.Select(r => new OeeRegressionRow
            {
                Availability = (float)r.Availability,
                Performance = (float)r.Performance,
                Quality = (float)r.Quality,
                StoppageCount = r.StoppageCount,
                StoppageDurationTotalSeconds = (float)r.StoppageDurationTotalSeconds,
                ProductionQuantity = r.ProductionQuantity,
                DefectQuantity = r.DefectQuantity,
                TargetCycleTime = (float)r.TargetCycleTime,
                OEE = (float)r.OEE
            }).ToList();

            var mlContext = new MLContext(seed: 0);
            var fullData = mlContext.Data.LoadFromEnumerable(data);

            var featurePipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(OeeRegressionInput.Availability),
                    nameof(OeeRegressionInput.Performance),
                    nameof(OeeRegressionInput.Quality),
                    nameof(OeeRegressionInput.StoppageCount),
                    nameof(OeeRegressionInput.StoppageDurationTotalSeconds),
                    nameof(OeeRegressionInput.ProductionQuantity),
                    nameof(OeeRegressionInput.DefectQuantity),
                    nameof(OeeRegressionInput.TargetCycleTime));

            IEstimator<ITransformer> pipeline = algorithm switch
            {
                MLAlgorithm.FastForest => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.FastForest(
                    labelColumnName: "Label", featureColumnName: "Features",
                    numberOfLeaves: 20, numberOfTrees: 100, minimumExampleCountPerLeaf: 10)),
                MLAlgorithm.Sdca => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "Label", featureColumnName: "Features",
                    maximumNumberOfIterations: 200)),
                _ => (IEstimator<ITransformer>)featurePipeline.Append(mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "Label", featureColumnName: "Features",
                    numberOfLeaves: 20, numberOfTrees: 100, minimumExampleCountPerLeaf: 10))
            };

            var split = mlContext.Data.TrainTestSplit(fullData, testFraction: (float)testFraction);

            _trainedModel = pipeline.Fit(split.TrainSet);
            _mlContext = mlContext;

            Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
            mlContext.Model.Save(_trainedModel, fullData.Schema, _modelPath);
            _logger.LogInformation("Regression model ({Algo}) saved to {Path}", algorithm, _modelPath);

            var predictions = _trainedModel.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            return new RegressionMetricsResult
            {
                RMSE = metrics.RootMeanSquaredError,
                MAE = metrics.MeanAbsoluteError,
                RSquared = metrics.RSquared,
                TrainingRows = (int)(split.TrainSet.GetRowCount() ?? 0),
                TestRows = (int)(split.TestSet.GetRowCount() ?? 0),
                AlgorithmName = AlgorithmLabel(algorithm)
            };
        }

        public async Task<RegressionMetricsResult?> EvaluateAsync(DateTime startUtc, DateTime endUtc, int? machineId = null)
        {
            if (!File.Exists(_modelPath))
                return null;

            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            if (rows.Count == 0) return null;

            var data = rows.Select(r => new OeeRegressionRow
            {
                Availability = (float)r.Availability,
                Performance = (float)r.Performance,
                Quality = (float)r.Quality,
                StoppageCount = r.StoppageCount,
                StoppageDurationTotalSeconds = (float)r.StoppageDurationTotalSeconds,
                ProductionQuantity = r.ProductionQuantity,
                DefectQuantity = r.DefectQuantity,
                TargetCycleTime = (float)r.TargetCycleTime,
                OEE = (float)r.OEE
            }).ToList();

            var mlContext = new MLContext(seed: 0);
            var dataView = mlContext.Data.LoadFromEnumerable(data);
            var model = mlContext.Model.Load(_modelPath, out var schema);
            var predictions = model.Transform(dataView);
            var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            return new RegressionMetricsResult
            {
                RMSE = metrics.RootMeanSquaredError,
                MAE = metrics.MeanAbsoluteError,
                RSquared = metrics.RSquared,
                TrainingRows = 0,
                TestRows = rows.Count
            };
        }

        public float? Predict(OeeRegressionInput input)
        {
            if (_mlContext == null || _trainedModel == null)
            {
                if (!File.Exists(_modelPath)) return null;
                _mlContext = new MLContext(seed: 0);
                _trainedModel = _mlContext.Model.Load(_modelPath, out _);
            }

            var predictor = _mlContext.Model.CreatePredictionEngine<OeeRegressionInput, OeeRegressionPrediction>(_trainedModel);
            var prediction = predictor.Predict(input);
            return prediction.PredictedOEE;
        }

        public float? PredictFromCsv(OeeCsvRow input)
        {
            if (!File.Exists(_modelPath)) return null;
            var mlContext = new MLContext(seed: 0);
            var model = mlContext.Model.Load(_modelPath, out _);
            var predictor = mlContext.Model.CreatePredictionEngine<OeeCsvRow, OeeCsvRegressionPrediction>(model);
            return predictor.Predict(input).PredictedOEE;
        }
    }
}
