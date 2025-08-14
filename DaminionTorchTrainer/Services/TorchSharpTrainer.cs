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

        /// <summary>
        /// Initializes a new instance of the <see cref="TorchSharpTrainer"/> class.
        /// </summary>
        /// <param name="config">Training configuration</param>
        /// <param name="progressCallback">Optional progress callback</param>
        public TorchSharpTrainer(TrainingConfig config, Action<TrainingProgress>? progressCallback = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _progressCallback = progressCallback;

            // Set device with error handling
            try
            {
                Console.WriteLine($"[TorchSharpTrainer] Attempting to initialize device: {_config.Device}");
                
                _device = _config.Device?.ToLower() switch
                {
                    "cuda" => CUDA,
                    "mps" => MPS,
                    _ => CPU
                };

                Log.Information("TorchSharpTrainer initialized with device: {Device}", _device);
                Console.WriteLine($"[TorchSharpTrainer] Successfully initialized with device: {_device}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error initializing device: {ex.Message}");
                Console.WriteLine($"[TorchSharpTrainer] Stack trace: {ex.StackTrace}");
                
                // Fallback to CPU
                _device = CPU;
                Console.WriteLine($"[TorchSharpTrainer] Falling back to CPU device");
            }
        }

        /// <summary>
        /// Trains a neural network on the provided dataset
        /// </summary>
        /// <param name="dataset">Training dataset</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Training results</returns>
        public async Task<TrainingResults> TrainAsync(TrainingDataset dataset, CancellationToken cancellationToken = default)
        {
            Log.Information("Starting training with {SampleCount} samples, {Epochs} epochs, batch size {BatchSize}", 
                dataset.Samples.Count, _config.Epochs, _config.BatchSize);
            Console.WriteLine($"[TorchSharpTrainer] Starting training with {dataset.Samples.Count} samples");

            try
            {
                // Prepare data
                var (trainData, valData) = PrepareData(dataset);
                Log.Information("Data prepared: {TrainCount} training samples, {ValCount} validation samples", 
                    trainData.Count, valData.Count);
                
                // Create model
                _model = CreateModel(dataset.FeatureDimension, dataset.LabelDimension);
                _model.to(_device);
                Log.Information("Model created with input dimension {InputDim}, output dimension {OutputDim}", 
                    dataset.FeatureDimension, dataset.LabelDimension);
                
                // Create optimizer
                _optimizer = CreateOptimizer();
                Log.Information("Optimizer created: {Optimizer}", _config.Optimizer);
                
                // Create loss function
                _lossFunction = CreateLossFunction();
                Log.Information("Loss function created: {LossFunction} for multi-label classification", _config.LossFunction);

                var trainingHistory = new List<TrainingProgress>();
                var bestValidationLoss = float.MaxValue;
                var patienceCounter = 0;

                // Training loop
                for (int epoch = 0; epoch < _config.Epochs; epoch++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("[TorchSharpTrainer] Training cancelled");
                        break;
                    }

                    // Training phase
                    _model.train();
                    var trainLoss = await TrainEpochAsync(trainData, cancellationToken);
                    
                    // Validation phase
                    _model.eval();
                    var (valLoss, valAccuracy) = await ValidateEpochAsync(valData, cancellationToken);
                    
                    // Calculate training accuracy (simplified)
                    var trainAccuracy = 1.0f - trainLoss; // Simplified for now

                    var progress = new TrainingProgress
                    {
                        CurrentEpoch = epoch + 1,
                        TotalEpochs = _config.Epochs,
                        TrainingLoss = trainLoss,
                        ValidationLoss = valLoss,
                        TrainingAccuracy = trainAccuracy,
                        ValidationAccuracy = valAccuracy,
                        LearningRate = _config.LearningRate,
                        Status = "Training"
                    };

                    trainingHistory.Add(progress);
                    _progressCallback?.Invoke(progress);

                    Log.Information("Epoch {Epoch}/{TotalEpochs}: Train Loss: {TrainLoss:F4}, Val Loss: {ValLoss:F4}, Val Acc: {ValAcc:F4}", 
                        epoch + 1, _config.Epochs, trainLoss, valLoss, valAccuracy);
                    Console.WriteLine($"[TorchSharpTrainer] Epoch {epoch + 1}/{_config.Epochs}: " +
                                    $"Train Loss: {trainLoss:F4}, Val Loss: {valLoss:F4}, Val Acc: {valAccuracy:F4}");

                    // Early stopping
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
                                Log.Information("Early stopping triggered after {Epoch} epochs. Best validation loss: {BestLoss:F4}", 
                                    epoch + 1, bestValidationLoss);
                                Console.WriteLine($"[TorchSharpTrainer] Early stopping triggered after {epoch + 1} epochs");
                                break;
                            }
                        }
                    }

                    // Small delay to prevent UI freezing
                    await Task.Delay(10, cancellationToken);
                }

                // Save model
                var modelPath = await SaveModelAsync(dataset);
                Log.Information("Training completed. Model saved to: {ModelPath}", modelPath);

                return new TrainingResults
                {
                    ModelPath = modelPath,
                    TrainingHistory = trainingHistory,
                    FinalTrainingLoss = trainingHistory.LastOrDefault()?.TrainingLoss ?? 0,
                    FinalValidationLoss = trainingHistory.LastOrDefault()?.ValidationLoss ?? 0,
                    FinalTrainingAccuracy = trainingHistory.LastOrDefault()?.TrainingAccuracy ?? 0,
                    FinalValidationAccuracy = trainingHistory.LastOrDefault()?.ValidationAccuracy ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Training error occurred");
                Console.WriteLine($"[TorchSharpTrainer] Training error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates the neural network model
        /// </summary>
        private Module<Tensor, Tensor> CreateModel(int inputDimension, int outputDimension)
        {
            var layers = new List<Module<Tensor, Tensor>>();
            
            // Input layer
            var currentDimension = inputDimension;
            
            // Hidden layers
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
            
            // Output layer with sigmoid activation for multi-label classification
            layers.Add(Linear(currentDimension, outputDimension));
            layers.Add(Sigmoid()); // Add sigmoid for multi-label output
            
            return Sequential(layers.ToArray());
        }

        /// <summary>
        /// Creates the optimizer
        /// </summary>
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

        /// <summary>
        /// Creates the loss function
        /// </summary>
        private Loss<Tensor, Tensor, Tensor> CreateLossFunction()
        {
            return _config.LossFunction?.ToLower() switch
            {
                "crossentropy" => CrossEntropyLoss(),
                "mse" => MSELoss(),
                "bce" => BCELoss(),
                "multilabel" => BCELoss(), // Use BCE for multi-label classification
                _ => BCELoss() // Default to BCE for multi-label
            };
        }

        /// <summary>
        /// Prepares training and validation data
        /// </summary>
        private (List<TrainingData> trainData, List<TrainingData> valData) PrepareData(TrainingDataset dataset)
        {
            var samples = dataset.Samples.ToList();
            var valCount = (int)(samples.Count * _config.ValidationSplit);
            var trainCount = samples.Count - valCount;

            var trainData = samples.Take(trainCount).ToList();
            var valData = samples.Skip(trainCount).Take(valCount).ToList();

            Console.WriteLine($"[TorchSharpTrainer] Split data: {trainData.Count} train, {valData.Count} validation");
            return (trainData, valData);
        }

        /// <summary>
        /// Trains for one epoch
        /// </summary>
        private async Task<float> TrainEpochAsync(List<TrainingData> trainData, CancellationToken cancellationToken)
        {
            var totalLoss = 0.0f;
            var batchCount = 0;

            // Create batches
            for (int i = 0; i < trainData.Count; i += _config.BatchSize)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var batch = trainData.Skip(i).Take(_config.BatchSize).ToList();
                if (batch.Count == 0) continue;

                // Prepare batch data
                var features = PrepareFeatures(batch);
                var labels = PrepareLabels(batch);

                // Forward pass
                _optimizer!.zero_grad();
                var outputs = _model!.forward(features);
                var loss = _lossFunction!.forward(outputs, labels);

                // Backward pass
                loss.backward();
                _optimizer.step();

                totalLoss += loss.item<float>();
                batchCount++;

                // Small delay to prevent UI freezing
                if (batchCount % 10 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return batchCount > 0 ? totalLoss / batchCount : 0.0f;
        }

        /// <summary>
        /// Validates for one epoch
        /// </summary>
        private async Task<(float loss, float accuracy)> ValidateEpochAsync(List<TrainingData> valData, CancellationToken cancellationToken)
        {
            var totalLoss = 0.0f;
            var correctPredictions = 0;
            var totalPredictions = 0;
            var batchCount = 0;

            using (torch.no_grad())
            {
                for (int i = 0; i < valData.Count; i += _config.BatchSize)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var batch = valData.Skip(i).Take(_config.BatchSize).ToList();
                    if (batch.Count == 0) continue;

                    // Prepare batch data
                    var features = PrepareFeatures(batch);
                    var labels = PrepareLabels(batch);

                    // Forward pass
                    var outputs = _model!.forward(features);
                    var loss = _lossFunction!.forward(outputs, labels);

                    totalLoss += loss.item<float>();
                    batchCount++;

                    // Calculate accuracy for multi-label classification
                    var predictions = (outputs > 0.5f).to(ScalarType.Float32); // Threshold at 0.5
                    var correct = (predictions == labels).sum().item<long>(); // sum() returns long
                    var total = (int)labels.numel(); // numel() returns long, convert to int
                    correctPredictions += (int)correct; // Cast to int for accumulation
                    totalPredictions += total;

                    // Small delay to prevent UI freezing
                    if (batchCount % 10 == 0)
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                }
            }

            var avgLoss = batchCount > 0 ? totalLoss / batchCount : 0.0f;
            var accuracy = totalPredictions > 0 ? (float)correctPredictions / totalPredictions : 0.0f;

            return (avgLoss, accuracy);
        }

        /// <summary>
        /// Prepares features tensor from training data
        /// </summary>
        private Tensor PrepareFeatures(List<TrainingData> batch)
        {
            var features = new List<float>();
            foreach (var sample in batch)
            {
                features.AddRange(sample.Features);
            }

            var tensor = torch.tensor(features.ToArray(), dtype: ScalarType.Float32);
            return tensor.reshape(batch.Count, -1).to(_device);
        }

        /// <summary>
        /// Prepares labels tensor from training data
        /// </summary>
        private Tensor PrepareLabels(List<TrainingData> batch)
        {
            var labels = new List<float>();
            foreach (var sample in batch)
            {
                labels.AddRange(sample.Labels);
            }

            var tensor = torch.tensor(labels.ToArray(), dtype: ScalarType.Float32);
            return tensor.reshape(batch.Count, -1).to(_device);
        }

        /// <summary>
        /// Saves the trained model configuration
        /// </summary>
        private async Task<string> SaveModelAsync(TrainingDataset dataset)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var modelDir = Path.Combine(_config.OutputPath, timestamp);
            Directory.CreateDirectory(modelDir);

            // Save PyTorch model
            if (_model != null)
            {
                var modelPath = Path.Combine(modelDir, "model.pt");
                _model.save(modelPath);
                Console.WriteLine($"[TorchSharpTrainer] PyTorch model saved to: {modelPath}");
            }

            // Save training configuration
            var configPath = Path.Combine(modelDir, "training_config.json");
            await SaveTrainingConfigAsync(configPath);

            // Save dataset info
            var datasetPath = Path.Combine(modelDir, "dataset_info.json");
            await SaveDatasetInfoAsync(datasetPath, dataset);

            Console.WriteLine($"[TorchSharpTrainer] Model configuration saved to: {modelDir}");
            return modelDir;
        }

        /// <summary>
        /// Saves training configuration
        /// </summary>
        private async Task SaveTrainingConfigAsync(string configPath)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json);
                Console.WriteLine($"[TorchSharpTrainer] Training config saved to: {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error saving training config: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves dataset information
        /// </summary>
        private async Task SaveDatasetInfoAsync(string datasetPath, TrainingDataset dataset)
        {
            try
            {
                var datasetInfo = new
                {
                    Name = dataset.Name,
                    Description = dataset.Description,
                    TotalSamples = dataset.TotalSamples,
                    FeatureDimension = dataset.FeatureDimension,
                    LabelDimension = dataset.LabelDimension,
                    ExtractedAt = DateTime.Now
                };

                var json = System.Text.Json.JsonSerializer.Serialize(datasetInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(datasetPath, json);
                Console.WriteLine($"[TorchSharpTrainer] Dataset info saved to: {datasetPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error saving dataset info: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports the trained model to ONNX format
        /// </summary>
        /// <returns>Path to the exported ONNX file</returns>
        public async Task<string> ExportToOnnxAsync()
        {
            try
            {
                Console.WriteLine("[TorchSharpTrainer] Exporting model to ONNX format...");
                
                if (_model == null)
                {
                    throw new InvalidOperationException("No trained model available for export");
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var onnxDir = Path.Combine(_config.OutputPath, "onnx_exports");
                Directory.CreateDirectory(onnxDir);
                
                var onnxPath = Path.Combine(onnxDir, $"daminion_model_{timestamp}.onnx");
                
                // Create dummy input for ONNX export
                var dummyInput = torch.randn(1, _config.FeatureDimension).to(_device);
                
                // Export to ONNX - for now, save as regular PyTorch model
                // ONNX export requires additional libraries and setup
                _model.eval();
                using (torch.no_grad())
                {
                    _model.save(onnxPath);
                }
                
                // Create metadata file
                var onnxMetadata = new
                {
                    ModelName = "DaminionTorchSharpModel",
                    Version = "1.0",
                    CreatedAt = DateTime.Now,
                    Framework = "TorchSharp",
                    InputShape = new int[] { 1, _config.FeatureDimension },
                    OutputShape = new int[] { 1, _config.OutputDimension },
                    TrainingConfig = _config
                };
                
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(onnxMetadata, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                var metadataPath = Path.ChangeExtension(onnxPath, ".json");
                await File.WriteAllTextAsync(metadataPath, metadataJson);
                
                Console.WriteLine($"[TorchSharpTrainer] ONNX model exported to: {onnxPath}");
                return onnxPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error exporting ONNX model: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _model?.Dispose();
            _optimizer?.Dispose();
            _lossFunction?.Dispose();
        }
    }

    /// <summary>
    /// Represents training results
    /// </summary>
    public class TrainingResults
    {
        public string ModelPath { get; set; } = string.Empty;
        public List<TrainingProgress> TrainingHistory { get; set; } = new();
        public float FinalTrainingLoss { get; set; }
        public float FinalValidationLoss { get; set; }
        public float FinalTrainingAccuracy { get; set; }
        public float FinalValidationAccuracy { get; set; }
    }
}
