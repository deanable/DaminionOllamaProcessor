using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using DaminionTorchTrainer.Models;
using Serilog;

namespace DaminionTorchTrainer.Services
{
    public class TorchSharpTrainer : IDisposable
    {
        private readonly TrainingConfig _config;
        private readonly Action<TrainingProgress>? _progressCallback;
        private Module<Tensor, Tensor>? _model;
        private torch.optim.Optimizer? _optimizer;
        private Loss<Tensor, Tensor, Tensor>? _lossFunction;
        private Device _device;
        private TrainingDataset? _currentDataset;

        public TorchSharpTrainer(TrainingConfig config, Action<TrainingProgress>? progressCallback = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _progressCallback = progressCallback;

            try
            {
                _device = _config.Device?.ToLower() switch
                {
                    "cuda" => CUDA,
                    "mps" => MPS,
                    _ => CPU
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error initializing device, falling back to CPU.");
                _device = CPU;
            }
        }

        public async Task<TrainingResults> TrainAsync(TrainingDataset dataset, CancellationToken cancellationToken = default)
        {
            _currentDataset = dataset;

            var (trainData, valData) = PrepareData(dataset);
            _model = CreateModel(dataset.FeatureDimension, dataset.LabelDimension);
            _model.to(_device);

            _optimizer = CreateOptimizer();
            _lossFunction = CreateLossFunction();

            var trainingHistory = new List<TrainingProgress>();
            var bestValidationLoss = float.MaxValue;
            var patienceCounter = 0;

            for (int epoch = 0; epoch < _config.Epochs; epoch++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _model.train();
                var trainLoss = await TrainEpochAsync(trainData, cancellationToken);

                _model.eval();
                var (valLoss, valAccuracy) = await ValidateEpochAsync(valData, cancellationToken);

                var progress = new TrainingProgress
                {
                    CurrentEpoch = epoch + 1,
                    TotalEpochs = _config.Epochs,
                    TrainingLoss = trainLoss,
                    ValidationLoss = valLoss,
                    TrainingAccuracy = 1.0f - trainLoss,
                    ValidationAccuracy = valAccuracy,
                };
                trainingHistory.Add(progress);
                _progressCallback?.Invoke(progress);

                if (_config.UseEarlyStopping && valLoss >= bestValidationLoss - _config.EarlyStoppingMinDelta)
                {
                    patienceCounter++;
                    if (patienceCounter >= _config.EarlyStoppingPatience)
                    {
                        Log.Information("Early stopping triggered.");
                        break;
                    }
                }
                else
                {
                    bestValidationLoss = valLoss;
                    patienceCounter = 0;
                }
            }

            var modelPath = await SaveModelAsync(dataset);
            return new TrainingResults
            {
                ModelPath = modelPath,
                TrainingHistory = trainingHistory,
                FinalTrainingLoss = trainingHistory.LastOrDefault()?.TrainingLoss ?? 0,
                FinalValidationLoss = trainingHistory.LastOrDefault()?.ValidationLoss ?? 0,
                FinalTrainingAccuracy = trainingHistory.LastOrDefault()?.TrainingAccuracy ?? 0,
                FinalValidationAccuracy = trainingHistory.LastOrDefault()?.ValidationAccuracy ?? 0,
                TrainingMethod = "TorchSharp",
                Algorithm = _config.ModelArchitecture ?? "Custom"
            };
        }

        private Module<Tensor, Tensor> CreateModel(int inputDim, int outputDim)
        {
            var layers = new List<Module<Tensor, Tensor>>();
            var currentDim = inputDim;
            
            foreach (var hiddenDim in _config.HiddenDimensions)
            {
                layers.Add(Linear(currentDim, hiddenDim));
                layers.Add(ReLU());
                if (_config.DropoutRate > 0) layers.Add(Dropout(_config.DropoutRate));
                currentDim = hiddenDim;
            }
            layers.Add(Linear(currentDim, outputDim));
            layers.Add(Sigmoid());
            return Sequential(layers.ToArray());
        }

        private torch.optim.Optimizer CreateOptimizer() => torch.optim.Adam(_model!.parameters(), _config.LearningRate, _config.WeightDecay);
        private Loss<Tensor, Tensor, Tensor> CreateLossFunction() => BCELoss();

        private (List<TrainingData> train, List<TrainingData> val) PrepareData(TrainingDataset dataset)
        {
            var shuffled = dataset.Samples.OrderBy(x => Guid.NewGuid()).ToList();
            var valCount = (int)(shuffled.Count * _config.ValidationSplit);
            return (shuffled.Skip(valCount).ToList(), shuffled.Take(valCount).ToList());
        }

        private async Task<float> TrainEpochAsync(List<TrainingData> data, CancellationToken token)
        {
            float totalLoss = 0;
            int batchCount = 0;
            foreach (var batch in data.Chunk(_config.BatchSize))
            {
                token.ThrowIfCancellationRequested();
                using var features = PrepareFeatures(batch).to(_device);
                using var labels = PrepareLabels(batch).to(_device);

                _optimizer!.zero_grad();
                var output = _model!.forward(features);
                var loss = _lossFunction!.forward(output, labels);
                loss.backward();
                _optimizer.step();
                totalLoss += loss.item<float>();
                batchCount++;
                await Task.Delay(1, token);
            }
            return totalLoss / batchCount;
        }

        private async Task<(float, float)> ValidateEpochAsync(List<TrainingData> data, CancellationToken token)
        {
            float totalLoss = 0;
            long correct = 0, total = 0;
            int batchCount = 0;
            using (torch.no_grad())
            {
                foreach (var batch in data.Chunk(_config.BatchSize))
                {
                    token.ThrowIfCancellationRequested();
                    using var features = PrepareFeatures(batch).to(_device);
                    using var labels = PrepareLabels(batch).to(_device);

                    var output = _model!.forward(features);
                    totalLoss += _lossFunction!.forward(output, labels).item<float>();
                    batchCount++;

                    var predicted = (output > 0.5f).to(ScalarType.Int64);
                    total += labels.numel();
                    correct += (predicted == labels.to(ScalarType.Int64)).sum().item<long>();
                    await Task.Delay(1, token);
                }
            }
            return (totalLoss / batchCount, (float)correct / total);
        }

        private Tensor PrepareFeatures(IEnumerable<TrainingData> batch) => torch.tensor(batch.SelectMany(s => s.Features).ToArray()).reshape(batch.Count(), -1);
        private Tensor PrepareLabels(IEnumerable<TrainingData> batch) => torch.tensor(batch.SelectMany(s => s.Labels).ToArray()).reshape(batch.Count(), -1);

        private async Task<string> SaveModelAsync(TrainingDataset dataset)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", timestamp);
            Directory.CreateDirectory(modelDir);
            
            var modelPath = Path.Combine(modelDir, "model.pt");
            _model.save(modelPath);
            
            var vocabPath = Path.Combine(modelDir, "vocabulary.json");
            var vocabJson = System.Text.Json.JsonSerializer.Serialize(dataset.MetadataVocabulary);
            await File.WriteAllTextAsync(vocabPath, vocabJson);
            
            return modelPath;
        }

        public async Task<string> ExportToOnnxAsync()
        {
            if (_model == null) throw new InvalidOperationException("Model not trained.");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var onnxDir = Path.Combine(_config.OutputPath, "onnx_exports");
            Directory.CreateDirectory(onnxDir);

            var onnxPath = Path.Combine(onnxDir, $"daminion_model_{timestamp}.onnx");

            _model.eval();
            _model.to(CPU);

            var ptPath = onnxPath.Replace(".onnx", ".pt");
            _model.save(ptPath);
            Log.Information("ONNX export not directly available. Model saved in PyTorch format at {Path}. Convert to ONNX using a separate Python script.", ptPath);

            _model.to(_device);

            var metadata = new { ModelName = "DaminionTorchSharpModel", Labels = _currentDataset?.MetadataVocabulary };
            var metadataPath = Path.ChangeExtension(onnxPath, ".json");
            await File.WriteAllTextAsync(metadataPath, System.Text.Json.JsonSerializer.Serialize(metadata));

            return onnxPath;
        }

        public void Dispose()
        {
            _model?.Dispose();
            _optimizer?.Dispose();
            _lossFunction?.Dispose();
        }
    }

    public class TrainingResults
    {
        public string ModelPath { get; set; } = string.Empty;
        public List<TrainingProgress> TrainingHistory { get; set; } = new();
        public float FinalTrainingLoss { get; set; }
        public float FinalValidationLoss { get; set; }
        public float FinalTrainingAccuracy { get; set; }
        public float FinalValidationAccuracy { get; set; }
        public string TrainingMethod { get; set; } = string.Empty;
        public string Algorithm { get; set; } = string.Empty;
    }
}
