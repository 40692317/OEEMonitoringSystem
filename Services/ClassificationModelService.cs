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
    public class ClassificationModelService : IClassificationModelService
    {
        private readonly IFeatureService _featureService;
        private readonly ILogger<ClassificationModelService> _logger;
        private readonly string _modelPath;
        private MLContext? _mlContext;
        private ITransformer? _trainedModel;

        public ClassificationModelService(
            IFeatureService featureService,
            ILogger<ClassificationModelService> logger,
            IWebHostEnvironment env)
        {
            _featureService = featureService;
            _logger = logger;
            _modelPath = Path.Combine(env.ContentRootPath, "MLModels", "OeeClassification.zip");
        }

        private static string AlgorithmLabel(MLAlgorithm alg) => alg switch
        {
            MLAlgorithm.FastForest => "Random Forest",
            MLAlgorithm.Sdca => "Logistic Regression",
            _ => "FastTree"
        };

        public Task<ClassificationMetricsResult> TrainFromCsvAsync(string csvPath, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default)
        {
            if (!File.Exists(csvPath))
            {
                _logger.LogWarning("CSV not found: {Path}", csvPath);
                return Task.FromResult(new ClassificationMetricsResult());
            }

            var mlContext = new MLContext(seed: 42);
            var rawData = mlContext.Data.LoadFromTextFile<OeeCsvRow>(
                csvPath, hasHeader: true, separatorChar: ',');

            _logger.LogInformation("CSV loaded for classification training ({Algo})", algorithm);

            var featurePipeline = mlContext.Transforms.Conversion.MapValueToKey("LabelKey", nameof(OeeCsvRow.State))
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
                MLAlgorithm.FastForest => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(
                        mlContext.BinaryClassification.Trainers.FastForest(
                            labelColumnName: "LabelKey", featureColumnName: "Features",
                            numberOfLeaves: 20, numberOfTrees: 100),
                        labelColumnName: "LabelKey"))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel")),
                MLAlgorithm.Sdca => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                        labelColumnName: "LabelKey", featureColumnName: "Features",
                        maximumNumberOfIterations: 200))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel")),
                _ => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(
                        mlContext.BinaryClassification.Trainers.FastTree(
                            labelColumnName: "LabelKey", featureColumnName: "Features",
                            numberOfLeaves: 20, numberOfTrees: 100),
                        labelColumnName: "LabelKey"))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"))
            };

            var split = mlContext.Data.TrainTestSplit(rawData, testFraction: (float)testFraction, seed: 42);
            var totalRows = (int)(rawData.GetRowCount() ?? 0);
            if (totalRows == 0)
                totalRows = File.ReadLines(csvPath).Count() - 1;
            var testRows = (int)Math.Round(totalRows * testFraction);
            var trainRows = totalRows - testRows;

            _trainedModel = pipeline.Fit(split.TrainSet);
            _mlContext = mlContext;

            SaveModelSafely(mlContext, _trainedModel, rawData.Schema, _modelPath);
            _logger.LogInformation("Classification model ({Algo}) saved to {Path}", algorithm, _modelPath);

            var predictions = _trainedModel.Transform(split.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(
                predictions, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            var result = new ClassificationMetricsResult
            {
                Accuracy = metrics.MacroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                LogLoss = metrics.LogLoss,
                TrainingRows = trainRows,
                TestRows = testRows,
                AlgorithmName = AlgorithmLabel(algorithm)
            };
            ExtractConfusionMatrix(metrics, result);
            return Task.FromResult(result);
        }

        public async Task<ClassificationMetricsResult> TrainAsync(DateTime startUtc, DateTime endUtc, int? machineId = null, double testFraction = 0.2, MLAlgorithm algorithm = MLAlgorithm.Default)
        {
            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            if (rows.Count < 10)
            {
                _logger.LogWarning("Insufficient data for classification training: {Count} rows.", rows.Count);
                return new ClassificationMetricsResult { TrainingRows = rows.Count, TestRows = 0 };
            }

            var data = rows.Select(r =>
            {
                var label = MachineStateLabelHelper.GetStateLabel(r.Availability, r.Performance, r.Quality, r.OEE);
                return new OeeClassificationRow
                {
                    Availability = (float)r.Availability,
                    Performance = (float)r.Performance,
                    Quality = (float)r.Quality,
                    StoppageCount = r.StoppageCount,
                    StoppageDurationTotalSeconds = (float)r.StoppageDurationTotalSeconds,
                    ProductionQuantity = r.ProductionQuantity,
                    DefectQuantity = r.DefectQuantity,
                    TargetCycleTime = (float)r.TargetCycleTime,
                    State = label
                };
            }).ToList();

            var mlContext = new MLContext(seed: 0);
            var fullData = mlContext.Data.LoadFromEnumerable(data);

            var featurePipeline = mlContext.Transforms.Conversion.MapValueToKey("LabelKey", "Label")
                .Append(mlContext.Transforms.Concatenate("Features",
                    nameof(OeeClassificationInput.Availability),
                    nameof(OeeClassificationInput.Performance),
                    nameof(OeeClassificationInput.Quality),
                    nameof(OeeClassificationInput.StoppageCount),
                    nameof(OeeClassificationInput.StoppageDurationTotalSeconds),
                    nameof(OeeClassificationInput.ProductionQuantity),
                    nameof(OeeClassificationInput.DefectQuantity),
                    nameof(OeeClassificationInput.TargetCycleTime)));

            IEstimator<ITransformer> pipeline = algorithm switch
            {
                MLAlgorithm.FastForest => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(
                        mlContext.BinaryClassification.Trainers.FastForest(
                            labelColumnName: "LabelKey", featureColumnName: "Features",
                            numberOfLeaves: 20, numberOfTrees: 100),
                        labelColumnName: "LabelKey"))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel")),
                MLAlgorithm.Sdca => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                        labelColumnName: "LabelKey", featureColumnName: "Features",
                        maximumNumberOfIterations: 200))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel")),
                _ => featurePipeline
                    .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(
                        mlContext.BinaryClassification.Trainers.FastTree(
                            labelColumnName: "LabelKey", featureColumnName: "Features",
                            numberOfLeaves: 20, numberOfTrees: 100),
                        labelColumnName: "LabelKey"))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"))
            };

            var split = mlContext.Data.TrainTestSplit(fullData, testFraction: (float)testFraction);
            _trainedModel = pipeline.Fit(split.TrainSet);
            _mlContext = mlContext;

            SaveModelSafely(mlContext, _trainedModel, fullData.Schema, _modelPath);
            _logger.LogInformation("Classification model ({Algo}) saved to {Path}", algorithm, _modelPath);

            var predictions = _trainedModel.Transform(split.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            var result = new ClassificationMetricsResult
            {
                Accuracy = metrics.MacroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                LogLoss = metrics.LogLoss,
                TrainingRows = (int)(split.TrainSet.GetRowCount() ?? 0),
                TestRows = (int)(split.TestSet.GetRowCount() ?? 0),
                AlgorithmName = AlgorithmLabel(algorithm)
            };
            ExtractConfusionMatrix(metrics, result);
            return result;
        }

        public async Task<ClassificationMetricsResult?> EvaluateAsync(DateTime startUtc, DateTime endUtc, int? machineId = null)
        {
            if (!File.Exists(_modelPath)) return null;

            var rows = await _featureService.GetFeaturesForPeriodAsync(startUtc, endUtc, machineId);
            if (rows.Count == 0) return null;

            var data = rows.Select(r => new OeeClassificationRow
            {
                Availability = (float)r.Availability,
                Performance = (float)r.Performance,
                Quality = (float)r.Quality,
                StoppageCount = r.StoppageCount,
                StoppageDurationTotalSeconds = (float)r.StoppageDurationTotalSeconds,
                ProductionQuantity = r.ProductionQuantity,
                DefectQuantity = r.DefectQuantity,
                TargetCycleTime = (float)r.TargetCycleTime,
                State = MachineStateLabelHelper.GetStateLabel(r.Availability, r.Performance, r.Quality, r.OEE)
            }).ToList();

            var mlContext = new MLContext(seed: 0);
            var dataView = mlContext.Data.LoadFromEnumerable(data);
            var model = mlContext.Model.Load(_modelPath, out _);
            var predictions = model.Transform(dataView);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            return new ClassificationMetricsResult
            {
                Accuracy = metrics.MacroAccuracy,
                MacroAccuracy = metrics.MacroAccuracy,
                LogLoss = metrics.LogLoss,
                TestRows = rows.Count
            };
        }

        public MachineState? Predict(OeeClassificationInput input)
        {
            if (_mlContext == null || _trainedModel == null)
            {
                if (!File.Exists(_modelPath)) return null;
                _mlContext = new MLContext(seed: 0);
                _trainedModel = _mlContext.Model.Load(_modelPath, out _);
            }

            var predictor = _mlContext.Model.CreatePredictionEngine<OeeClassificationInput, OeeClassificationPrediction>(_trainedModel);
            var prediction = predictor.Predict(input);
            return ParseState(prediction.PredictedState);
        }

        public MachineState? PredictFromCsv(OeeCsvRow input)
        {
            if (!File.Exists(_modelPath)) return null;
            var mlContext = new MLContext(seed: 0);
            var model = mlContext.Model.Load(_modelPath, out _);
            var predictor = mlContext.Model.CreatePredictionEngine<OeeCsvRow, OeeCsvClassificationPrediction>(model);
            return ParseState(predictor.Predict(input).PredictedState);
        }

        public MachineState GetStateFromRules(decimal availability, decimal performance, decimal quality, decimal oee)
        {
            return MachineStateLabelHelper.GetState(availability, performance, quality, oee);
        }

        private static void ExtractConfusionMatrix(
            Microsoft.ML.Data.MulticlassClassificationMetrics metrics,
            ClassificationMetricsResult result)
        {
            var cm = metrics.ConfusionMatrix;
            var counts = cm.Counts;
            result.ConfusionMatrixCounts = new List<List<double>>();
            for (int i = 0; i < counts.Count; i++)
            {
                var row = new List<double>();
                for (int j = 0; j < counts[i].Count; j++)
                    row.Add(counts[i][j]);
                result.ConfusionMatrixCounts.Add(row);
            }

            // Parse actual class labels from the formatted confusion table
            var labels = new List<string>();
            var table = cm.GetFormattedConfusionTable();
            foreach (var line in table.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.Contains("||")) continue;
                var left = trimmed.Split("||")[0].Trim();
                if (string.IsNullOrWhiteSpace(left)) continue;
                if (left.StartsWith("PREDICTED", StringComparison.OrdinalIgnoreCase)) continue;
                if (left.StartsWith("TRUTH", StringComparison.OrdinalIgnoreCase)) continue;
                if (left.StartsWith("Precision", StringComparison.OrdinalIgnoreCase)) continue;
                if (left.All(c => c == '=' || c == '-')) continue;
                labels.Add(left);
            }
            if (labels.Count != counts.Count)
            {
                labels = Enumerable.Range(0, counts.Count)
                    .Select(i => i switch { 0 => "Normal", 1 => "Warning", 2 => "Critical", _ => $"Class {i}" })
                    .ToList();
            }
            result.ClassLabels = labels;
        }

        private static MachineState? ParseState(string? s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Trim() switch
            {
                "Normal" => MachineState.Normal,
                "Warning" => MachineState.Warning,
                "Critical" => MachineState.Critical,
                _ => null
            };
        }

        private void SaveModelSafely(MLContext mlContext, ITransformer model, DataViewSchema schema, string targetPath)
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = targetPath + ".tmp";
            const int maxRetries = 5;
            const int retryDelayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);

                    mlContext.Model.Save(model, schema, tempPath);

                    if (File.Exists(targetPath))
                        File.Delete(targetPath);

                    File.Move(tempPath, targetPath);
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning("Model save attempt {Attempt}/{Max} failed, retrying: {Message}", attempt, maxRetries, ex.Message);
                    Thread.Sleep(retryDelayMs * attempt);
                }
            }

            throw new IOException($"Failed to save model after {maxRetries} attempts.");
        }
    }
}