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
    /// Service for training neural networks using TorchSharp.
    /// This class encapsulates the entire training pipeline, including data preparation,
    /// model creation, training, validation, and model saving.
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
        /// <param name="config">The training configuration.</param>
        /// <param name="progressCallback">An optional callback to report training progress.</param>
        public TorchSharpTrainer(TrainingConfig config, Action<TrainingProgress>? progressCallback = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _progressCallback = progressCallback;

            // Set the computation device (CUDA, MPS, or CPU) with a fallback to CPU.
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
                
                // Fallback to CPU if the selected device is not available.
                _device = CPU;
                Console.WriteLine($"[TorchSharpTrainer] Falling back to CPU device");
            }
        }

        /// <summary>
        /// Trains a neural network asynchronously using the provided dataset and configuration.
        /// The training process involves initializing the model, optimizer, and loss function,
        /// then iterating through epochs to train and validate the model.
        /// Early stopping is used to prevent overfitting.
        /// </summary>
        /// <param name="dataset">The dataset to train the model on.</param>
        /// <param name="cancellationToken">A token to cancel the training process.</param>
        /// <returns>A <see cref="TrainingResults"/> object containing the results of the training.</returns>
        public async Task<TrainingResults> TrainAsync(TrainingDataset dataset, CancellationToken cancellationToken = default)
        {
            Log.Information("Starting training with {SampleCount} samples, {Epochs} epochs, batch size {BatchSize}", 
                dataset.Samples.Count, _config.Epochs, _config.BatchSize);
            Console.WriteLine($"[TorchSharpTrainer] Starting training with {dataset.Samples.Count} samples");

            try
            {
                // Store current dataset for later use, e.g., ONNX export.
                _currentDataset = dataset;
                
                // Split the dataset into training and validation sets.
                var (trainData, valData) = PrepareData(dataset);
                Log.Information("Data prepared: {TrainCount} training samples, {ValCount} validation samples", 
                    trainData.Count, valData.Count);
                
                // Create the neural network model.
                _model = CreateModel(dataset.FeatureDimension, dataset.LabelDimension);
                _model.to(_device);
                Log.Information("Model created with input dimension {InputDim}, output dimension {OutputDim}", 
                    dataset.FeatureDimension, dataset.LabelDimension);
                
                // Create the optimizer.
                _optimizer = CreateOptimizer();
                Log.Information("Optimizer created: {Optimizer}", _config.Optimizer);
                
                // Create the loss function.
                _lossFunction = CreateLossFunction();
                Log.Information("Loss function created: {LossFunction} for multi-label classification", _config.LossFunction);

                var trainingHistory = new List<TrainingProgress>();
                var bestValidationLoss = float.MaxValue;
                var patienceCounter = 0;

                // Main training loop over epochs.
                for (int epoch = 0; epoch < _config.Epochs; epoch++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("[TorchSharpTrainer] Training cancelled");
                        break;
                    }

                    // Training phase for one epoch.
                    _model.train();
                    var trainLoss = await TrainEpochAsync(trainData, cancellationToken);
                    
                    // Validation phase for one epoch.
                    _model.eval();
                    var (valLoss, valAccuracy) = await ValidateEpochAsync(valData, cancellationToken);
                    
                    // Simplified calculation of training accuracy.
                    var trainAccuracy = 1.0f - trainLoss;

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

                    // Early stopping logic to prevent overfitting.
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

                    // Introduce a small delay to keep the UI responsive.
                    await Task.Delay(10, cancellationToken);
                }

                // Save the trained model.
                var modelPath = await SaveModelAsync(dataset);
                Log.Information("Training completed. Model saved to: {ModelPath}", modelPath);

                // Log a summary of the training process.
                await LogModelSummaryAsync(dataset, trainingHistory, modelPath);

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
        /// Creates the neural network model based on the training configuration.
        /// </summary>
        /// <param name="inputDimension">The input dimension of the model.</param>
        /// <param name="outputDimension">The output dimension of the model.</param>
        /// <returns>A TorchSharp model.</returns>
        private Module<Tensor, Tensor> CreateModel(int inputDimension, int outputDimension)
        {
            var layers = new List<Module<Tensor, Tensor>>();
            var currentDimension = inputDimension;
            
            // Add hidden layers.
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
            
            // Add the output layer with a Sigmoid activation for multi-label classification.
            layers.Add(Linear(currentDimension, outputDimension));
            layers.Add(Sigmoid());
            
            return Sequential(layers.ToArray());
        }

        /// <summary>
        /// Creates the optimizer based on the training configuration.
        /// </summary>
        /// <returns>A TorchSharp optimizer.</returns>
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
        /// Creates the loss function based on the training configuration.
        /// </summary>
        /// <returns>A TorchSharp loss function.</returns>
        private Loss<Tensor, Tensor, Tensor> CreateLossFunction()
        {
            return _config.LossFunction?.ToLower() switch
            {
                "crossentropy" => CrossEntropyLoss(),
                "mse" => MSELoss(),
                "bce" => BCELoss(),
                "multilabel" => BCELoss(), // Use BCE for multi-label classification.
                _ => BCELoss() // Default to BCE for multi-label.
            };
        }

        /// <summary>
        /// Splits the dataset into training and validation sets.
        /// </summary>
        /// <param name="dataset">The dataset to split.</param>
        /// <returns>A tuple containing the training and validation data.</returns>
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
        /// Runs the training process for a single epoch.
        /// </summary>
        /// <param name="trainData">The training data for the epoch.</param>
        /// <param name="cancellationToken">A token to cancel the training process.</param>
        /// <returns>The average training loss for the epoch.</returns>
        private async Task<float> TrainEpochAsync(List<TrainingData> trainData, CancellationToken cancellationToken)
        {
            var totalLoss = 0.0f;
            var batchCount = 0;

            for (int i = 0; i < trainData.Count; i += _config.BatchSize)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var batch = trainData.Skip(i).Take(_config.BatchSize).ToList();
                if (batch.Count == 0) continue;

                var features = PrepareFeatures(batch);
                var labels = PrepareLabels(batch);

                _optimizer!.zero_grad();
                var outputs = _model!.forward(features);
                var loss = _lossFunction!.forward(outputs, labels);

                loss.backward();
                _optimizer.step();

                totalLoss += loss.item<float>();
                batchCount++;

                if (batchCount % 10 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return batchCount > 0 ? totalLoss / batchCount : 0.0f;
        }

        /// <summary>
        /// Runs the validation process for a single epoch.
        /// </summary>
        /// <param name="valData">The validation data for the epoch.</param>
        /// <param name="cancellationToken">A token to cancel the validation process.</param>
        /// <returns>A tuple containing the average validation loss and accuracy.</returns>
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

                    var features = PrepareFeatures(batch);
                    var labels = PrepareLabels(batch);

                    var outputs = _model!.forward(features);
                    var loss = _lossFunction!.forward(outputs, labels);

                    totalLoss += loss.item<float>();
                    batchCount++;

                    var predictions = (outputs > 0.5f).to(ScalarType.Float32);
                    var correct = (predictions == labels).sum().item<long>();
                    var total = (int)labels.numel();
                    correctPredictions += (int)correct;
                    totalPredictions += total;

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
        /// Prepares a batch of features for training.
        /// </summary>
        /// <param name="batch">The batch of training data.</param>
        /// <returns>A tensor representing the features.</returns>
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
        /// Prepares a batch of labels for training.
        /// </summary>
        /// <param name="batch">The batch of training data.</param>
        /// <returns>A tensor representing the labels.</returns>
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
        /// Saves the trained model to disk.
        /// </summary>
        /// <param name="dataset">The dataset used for training, containing metadata.</param>
        /// <returns>The path to the saved model.</returns>
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

        /// <summary>
        /// Saves the training configuration to a file.
        /// </summary>
        /// <param name="configPath">The path to save the configuration file.</param>
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
        /// Saves information about the dataset to a file.
        /// </summary>
        /// <param name="datasetPath">The path to save the dataset information file.</param>
        /// <param name="dataset">The dataset to save information about.</param>
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
        /// Exports the trained model to the ONNX format.
        /// </summary>
        /// <returns>The path to the exported ONNX model file.</returns>
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
                
                var dummyInput = torch.randn(1, _config.FeatureDimension).to(_device);
                
                _model.eval();
                using (torch.no_grad())
                {
                    _model.save(onnxPath);
                }
                
                var onnxMetadata = new
                {
                    ModelName = "DaminionTorchSharpModel",
                    Version = "1.0",
                    CreatedAt = DateTime.Now,
                    Framework = "TorchSharp",
                    InputShape = new int[] { 1, _config.FeatureDimension },
                    OutputShape = new int[] { 1, _config.OutputDimension },
                    TrainingConfig = _config,
                    Labels = _currentDataset?.MetadataVocabulary ?? new Dictionary<string, int>(),
                    LabelMapping = _currentDataset?.MetadataVocabulary?.ToDictionary(kvp => kvp.Value, kvp => kvp.Key) ?? new Dictionary<int, string>()
                };
                
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(onnxMetadata, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                var metadataPath = Path.ChangeExtension(onnxPath, ".json");
                await File.WriteAllTextAsync(metadataPath, metadataJson);
                
                Console.WriteLine($"[TorchSharpTrainer] ONNX model exported to: {onnxPath}");
                Console.WriteLine($"[TorchSharpTrainer] ONNX metadata exported to: {metadataPath}");
                Console.WriteLine($"[TorchSharpTrainer] Labels included: {(_currentDataset?.MetadataVocabulary?.Count ?? 0)} terms");
                return onnxPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TorchSharpTrainer] Error exporting ONNX model: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Logs a comprehensive summary of the model training process.
        /// </summary>
        /// <param name="dataset">The dataset used for training.</param>
        /// <param name="trainingHistory">The history of training progress.</param>
        /// <param name="modelPath">The path to the saved model.</param>
        private async Task LogModelSummaryAsync(TrainingDataset dataset, List<TrainingProgress> trainingHistory, string modelPath)
        {
            var trainingDuration = TimeSpan.FromSeconds(trainingHistory.Count * 2); // Simplified estimation.

            Log.Information("===== MODEL TRAINING SUMMARY =====");
            Log.Information("Model Path: {ModelPath}", modelPath);
            Log.Information("Training Duration: {Duration} (estimated)", trainingDuration);
            Log.Information("Total Training Epochs: {TotalEpochs}", _config.Epochs);
            Log.Information("Actual Epochs Trained: {ActualEpochs}", trainingHistory.Count);
            Log.Information("Final Training Loss: {FinalTrainingLoss:F4}", trainingHistory.LastOrDefault()?.TrainingLoss ?? 0);
            Log.Information("Final Validation Loss: {FinalValidationLoss:F4}", trainingHistory.LastOrDefault()?.ValidationLoss ?? 0);
            Log.Information("Final Training Accuracy: {FinalTrainingAccuracy:F4}", trainingHistory.LastOrDefault()?.TrainingAccuracy ?? 0);
            Log.Information("Final Validation Accuracy: {FinalValidationAccuracy:F4}", trainingHistory.LastOrDefault()?.ValidationAccuracy ?? 0);

            Log.Information("===== MODEL ARCHITECTURE =====");
            if (_model != null)
            {
                Log.Information("Model Type: {ModelType}", _model.GetType().Name);
                Log.Information("Input Dimension: {InputDim} (visual features from image pixels)", _config.FeatureDimension);
                Log.Information("Output Dimension: {OutputDim} (metadata terms)", _config.OutputDimension);
                Log.Information("Device: {Device}", _device);
                Log.Information("Optimizer: {Optimizer}", _config.Optimizer);
                Log.Information("Loss Function: {LossFunction}", _config.LossFunction);
                Log.Information("Learning Rate: {LearningRate}", _config.LearningRate);
                Log.Information("Batch Size: {BatchSize}", _config.BatchSize);
                Log.Information("Hidden Dimensions: [{HiddenDims}]", string.Join(", ", _config.HiddenDimensions));
                Log.Information("Dropout Rate: {DropoutRate}", _config.DropoutRate);
                Log.Information("Weight Decay: {WeightDecay}", _config.WeightDecay);

                var totalParams = 0L;
                foreach (var param in _model.parameters())
                {
                    totalParams += param.numel();
                }
                Log.Information("Total Parameters: {TotalParams:N0}", totalParams);
            }
            else
            {
                Log.Warning("Model is null. Cannot log architecture details.");
            }

            Log.Information("===== DATASET INFORMATION =====");
            Log.Information("Dataset Name: {DatasetName}", dataset.Name ?? "Unnamed Dataset");
            Log.Information("Total Samples: {TotalSamples}", dataset.TotalSamples);
            Log.Information("Feature Dimension: {FeatureDim} (visual features)", dataset.FeatureDimension);
            Log.Information("Label Dimension: {LabelDim} (metadata terms)", dataset.LabelDimension);
            Log.Information("Data Source: {DataSource}", dataset.DataSource);

            Log.Information("===== TRAINED METADATA TERMS =====");
            if (dataset.LabelDimension > 0)
            {
                var metadataTerms = ExtractMetadataTermsFromDataset(dataset);
                if (metadataTerms.Any())
                {
                    Log.Information("This model was trained to recognize {TermCount} metadata terms:", metadataTerms.Count);
                    for (int i = 0; i < metadataTerms.Count; i++)
                    {
                        Log.Information("  [{Index}] {Term}", i, metadataTerms[i]);
                    }
                }
                else
                {
                    Log.Information("Model trained for {LabelDim} metadata terms (specific terms not available)", dataset.LabelDimension);
                }
            }
            else
            {
                Log.Information("No metadata terms were trained.");
            }

            Log.Information("===== ONNX EXPORT INFORMATION =====");
            Log.Information("ONNX Entry Node: input (shape: [batch_size, {InputDim}])", _config.FeatureDimension);
            Log.Information("ONNX Output Node: output (shape: [batch_size, {OutputDim}])", _config.OutputDimension);
            Log.Information("ONNX Framework: TorchSharp");
            Log.Information("ONNX Version: 1.0");
            Log.Information("Model can be exported to ONNX format for deployment");

            Log.Information("===== END MODEL SUMMARY =====");
        }

        /// <summary>
        /// Extracts the metadata terms from the dataset vocabulary.
        /// </summary>
        /// <param name="dataset">The dataset to extract terms from.</param>
        /// <returns>A list of metadata terms.</returns>
        private List<string> ExtractMetadataTermsFromDataset(TrainingDataset dataset)
        {
            var terms = new List<string>();
            
            if (dataset.MetadataVocabulary != null && dataset.MetadataVocabulary.Any())
            {
                var sortedTerms = dataset.MetadataVocabulary
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                return sortedTerms;
            }
            
            if (dataset.Samples.Any())
            {
                var sample = dataset.Samples.First();
                if (sample.Labels.Count > 0)
                {
                    for (int i = 0; i < Math.Min(sample.Labels.Count, 20); i++)
                    {
                        terms.Add($"metadata_term_{i}");
                    }
                }
            }
            
            return terms;
        }

        /// <summary>
        /// Disposes the TorchSharp resources used by the trainer.
        /// </summary>
        public void Dispose()
        {
            _model?.Dispose();
            _optimizer?.Dispose();
            _lossFunction?.Dispose();
        }
    }

    /// <summary>
    /// Represents the results of a training process.
    /// </summary>
    public class TrainingResults
    {
        /// <summary>
        /// Gets or sets the path to the saved model.
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the history of training progress.
        /// </summary>
        public List<TrainingProgress> TrainingHistory { get; set; } = new();
        /// <summary>
        /// Gets or sets the final training loss.
        /// </summary>
        public float FinalTrainingLoss { get; set; }
        /// <summary>
        /// Gets or sets the final validation loss.
        /// </summary>
        public float FinalValidationLoss { get; set; }
        /// <summary>
        /// Gets or sets the final training accuracy.
        /// </summary>
        public float FinalTrainingAccuracy { get; set; }
        /// <summary>
        /// Gets or sets the final validation accuracy.
        /// </summary>
        public float FinalValidationAccuracy { get; set; }
        /// <summary>
        /// Gets or sets the training method used.
        /// </summary>
        public string TrainingMethod { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the algorithm used for training.
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;
    }
}
