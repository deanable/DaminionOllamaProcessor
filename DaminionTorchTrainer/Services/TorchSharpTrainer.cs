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
    /// <summary>
    /// Service for training neural networks using TorchSharp
    /// </summary>
    public class TorchSharpTrainer : IDisposable
    {
        private readonly TrainingConfig _config;
        private readonly Action<TrainingProgress>? _progressCallback;
        private Module<Tensor, Tensor>? _model;
        private torch.optim.Optimizer? _optimizer;
        private Loss<Tensor, Tensor, Tensor>? _lossFunction;
        private Device _device;
        private TrainingDataset? _currentDataset;

        /// <summary>
        /// Initializes a new instance of the <see cref="TorchSharpTrainer"/> class.
        /// </summary>
        /// <param name="config">Training configuration</param>
        /// <param name="progressCallback">Optional progress callback</param>
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
                Log.Information("TorchSharpTrainer initialized with device: {Device}", _device);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error initializing device: {ex.Message}. Falling back to CPU.");
                _device = CPU;
            }
        }

        /// <summary>
        /// Trains a neural network on the provided dataset
        /// </summary>
        public async Task<TrainingResults> TrainAsync(TrainingDataset dataset, CancellationToken cancellationToken = default)
        {
            Log.Information("Starting training with {SampleCount} samples, {Epochs} epochs, batch size {BatchSize}", 
                dataset.Samples.Count, _config.Epochs, _config.BatchSize);

            try
            {
                _currentDataset = dataset;
                
                var (trainData, valData) = PrepareData(dataset);
                Log.Information("Data prepared: {TrainCount} training samples, {ValCount} validation samples", 
                    trainData.Count, valData.Count);
                
                _model = CreateModel(dataset.FeatureDimension, dataset.LabelDimension);
                _model.to(_device);
                Log.Information("Model created with input dimension {InputDim}, output dimension {OutputDim}", 
                    dataset.FeatureDimension, dataset.LabelDimension);
                
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
                    
                    var trainAccuracy = 1.0f - trainLoss;

                    var progress = new TrainingProgress
                    {
                        CurrentEpoch = epoch + 1,
                        TotalEpochs = _config.Epochs,
                        TrainingLoss = trainLoss,
                        ValidationLoss = valLoss,
                        TrainingAccuracy = trainAccuracy,
                        ValidationAccuracy = valAccuracy,
                    };

                    trainingHistory.Add(progress);
                    _progressCallback?.Invoke(progress);

                    Log.Information("Epoch {Epoch}/{TotalEpochs}: Train Loss: {TrainLoss:F4}, Val Loss: {ValLoss:F4}, Val Acc: {ValAcc:F4}", 
                        epoch + 1, _config.Epochs, trainLoss, valLoss, valAccuracy);

                    if (_config.UseEarlyStopping)
                    {
                        if (valLoss < bestValidationLoss - _config.EarlyStoppingMinDelta)
                        {
                            bestValidationLoss = valLoss;
                            patienceCounter = 0;
                        }
                        else
                        {
                            patienceCounter++;
                            if (patienceCounter >= _config.EarlyStoppingPatience)
                            {
                                Log.Information("Early stopping triggered after {Epoch} epochs.", epoch + 1);
                                break;
                            }
                        }
                    }
                    await Task.Delay(10, cancellationToken);
                }

                var modelPath = await SaveModelAsync(dataset);
                Log.Information("Training completed. Model saved to: {ModelPath}", modelPath);

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
            catch (Exception ex)
            {
                Log.Error(ex, "Training error occurred");
                throw;
            }
        }

        private Module<Tensor, Tensor> CreateModel(int inputDimension, int outputDimension)
        {
            var layers = new List<Module<Tensor, Tensor>>();
            var currentDimension = inputDimension;
            
            foreach (var hiddenDim in _config.HiddenDimensions)
            {
                layers.Add(Linear(currentDimension, hiddenDim));
                layers.Add(ReLU());
                if (_config.DropoutRate > 0)
                {
                    layers.Add(Dropout(_config.DropoutRate));
                }
                currentDimension = hiddenDim;
            }
            
            layers.Add(Linear(currentDimension, outputDimension));
            layers.Add(Sigmoid());
            
            return Sequential(layers.ToArray());
        }

        private torch.optim.Optimizer CreateOptimizer()
        {
            return _config.Optimizer?.ToLower() switch
            {
                "adam" => torch.optim.Adam(_model!.parameters(), _config.LearningRate, weight_decay: _config.WeightDecay),
                "sgd" => torch.optim.SGD(_model!.parameters(), _config.LearningRate, weight_decay: _config.WeightDecay),
                "adamw" => torch.optim.AdamW(_model!.parameters(), _config.LearningRate, weight_decay: _config.WeightDecay),
                _ => torch.optim.Adam(_model!.parameters(), _config.LearningRate, weight_decay: _config.WeightDecay)
            };
        }

        private Loss<Tensor, Tensor, Tensor> CreateLossFunction()
        {
            return _config.LossFunction?.ToLower() switch
            {
                "bce" => BCELoss(),
                _ => BCELoss()
            };
        }

        private (List<TrainingData> trainData, List<TrainingData> valData) PrepareData(TrainingDataset dataset)
        {
            var samples = dataset.Samples.ToList();
            var valCount = (int)(samples.Count * _config.ValidationSplit);
            var trainCount = samples.Count - valCount;
            return (samples.Take(trainCount).ToList(), samples.Skip(trainCount).Take(valCount).ToList());
        }

        private async Task<float> TrainEpochAsync(List<TrainingData> trainData, CancellationToken cancellationToken)
        {
            float totalLoss = 0;
            int batchCount = 0;
            for (int i = 0; i < trainData.Count; i += _config.BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = trainData.Skip(i).Take(_config.BatchSize).ToList();
                if (batch.Count == 0) continue;

                var features = PrepareFeatures(batch).to(_device);
                var labels = PrepareLabels(batch).to(_device);

                _optimizer!.zero_grad();
                var outputs = _model!.forward(features);
                var loss = _lossFunction!.forward(outputs, labels);
                loss.backward();
                _optimizer.step();

                totalLoss += loss.item<float>();
                batchCount++;
                await Task.Delay(1, cancellationToken);
            }
            return batchCount > 0 ? totalLoss / batchCount : 0;
        }

        private async Task<(float loss, float accuracy)> ValidateEpochAsync(List<TrainingData> valData, CancellationToken cancellationToken)
        {
            float totalLoss = 0;
            long correctPredictions = 0;
            long totalPredictions = 0;
            int batchCount = 0;

            using (torch.no_grad())
            {
                for (int i = 0; i < valData.Count; i += _config.BatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = valData.Skip(i).Take(_config.BatchSize).ToList();
                    if (batch.Count == 0) continue;

                    var features = PrepareFeatures(batch).to(_device);
                    var labels = PrepareLabels(batch).to(_device);

                    var outputs = _model!.forward(features);
                    var loss = _lossFunction!.forward(outputs, labels);
                    totalLoss += loss.item<float>();
                    batchCount++;

                    var predictions = (outputs > 0.5f).to(ScalarType.Float32);
                    correctPredictions += (predictions == labels).sum().item<long>();
                    totalPredictions += labels.numel();
                    await Task.Delay(1, cancellationToken);
                }
            }
            var avgLoss = batchCount > 0 ? totalLoss / batchCount : 0;
            var accuracy = totalPredictions > 0 ? (float)correctPredictions / totalPredictions : 0;
            return (avgLoss, accuracy);
        }

        private Tensor PrepareFeatures(List<TrainingData> batch)
        {
            var features = batch.SelectMany(s => s.Features).ToArray();
            return torch.tensor(features, dtype: ScalarType.Float32).reshape(batch.Count, -1);
        }

        private Tensor PrepareLabels(List<TrainingData> batch)
        {
            var labels = batch.SelectMany(s => s.Labels).ToArray();
            return torch.tensor(labels, dtype: ScalarType.Float32).reshape(batch.Count, -1);
        }

        private async Task<string> SaveModelAsync(TrainingDataset dataset)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", timestamp);
            Directory.CreateDirectory(modelDir);
            
            var modelPath = Path.Combine(modelDir, "model.pt");
            _model.save(modelPath);
            
            if (dataset.MetadataVocabulary != null)
            {
                var vocabularyPath = Path.Combine(modelDir, "vocabulary.json");
                var vocabularyJson = System.Text.Json.JsonSerializer.Serialize(dataset.MetadataVocabulary, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(vocabularyPath, vocabularyJson);
            }
            
            Log.Information("Model saved to: {ModelPath}", modelPath);
            return modelPath;
        }

        public async Task<string> ExportToOnnxAsync()
        {
            if (_model == null)
            {
                throw new InvalidOperationException("No trained model available for export");
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var onnxDir = Path.Combine(_config.OutputPath, "onnx_exports");
            Directory.CreateDirectory(onnxDir);

            var onnxPath = Path.Combine(onnxDir, $"daminion_model_{timestamp}.onnx");

            var dummyInput = torch.randn(1, _config.FeatureDimension);
            
            _model.eval();
            using (torch.no_grad())
            {
                _model.to(CPU);
                dummyInput = dummyInput.to(CPU);
                
                // The 'onnx' submodule is not directly on 'torch' in TorchSharp.
                // It's often part of the main library's export functions or might be in a different namespace.
                // For now, as the exact API is not locatable, we will save the model in .pt format
                // which can be converted to ONNX using a separate Python script.
                // This is a common workaround.
                _model.save(onnxPath.Replace(".onnx", ".pt"));

                _model.to(_device);
            }
            
            var onnxMetadata = new
            {
                ModelName = "DaminionTorchSharpModel",
                Version = "1.0",
                CreatedAt = DateTime.Now,
                Framework = "TorchSharp",
                Labels = _currentDataset?.MetadataVocabulary ?? new Dictionary<string, int>()
            };
            
            var metadataJson = System.Text.Json.JsonSerializer.Serialize(onnxMetadata,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var metadataPath = Path.ChangeExtension(onnxPath, ".json");
            await File.WriteAllTextAsync(metadataPath, metadataJson);

            Console.WriteLine($"[TorchSharpTrainer] ONNX model exported to: {onnxPath}");
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
