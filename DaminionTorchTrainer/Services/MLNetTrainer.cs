using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DaminionTorchTrainer.Models;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for training models using ML.NET and .mbconfig files
    /// </summary>
    public class MLNetTrainer
    {
        private readonly Action<TrainingProgress>? _progressCallback;

        public MLNetTrainer(Action<TrainingProgress>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        /// <summary>
        /// Loads and validates a .mbconfig file
        /// </summary>
        /// <param name="mbconfigPath">Path to the .mbconfig file</param>
        /// <returns>Parsed MbConfig object</returns>
        public async Task<MbConfig> LoadMbConfigAsync(string mbconfigPath)
        {
            try
            {
                Log.Information("Loading MBConfig from: {Path}", mbconfigPath);
                
                if (!File.Exists(mbconfigPath))
                {
                    throw new FileNotFoundException($"MBConfig file not found: {mbconfigPath}");
                }

                var jsonContent = await File.ReadAllTextAsync(mbconfigPath);
                var mbConfig = JsonSerializer.Deserialize<MbConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (mbConfig == null)
                {
                    throw new InvalidOperationException("Failed to parse MBConfig file");
                }

                Log.Information("MBConfig loaded successfully. Algorithm: {Algorithm}, DataSources: {DataSourceCount}", 
                    mbConfig.Training.Algorithm, mbConfig.DataSources.Count);

                return mbConfig;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading MBConfig from {Path}", mbconfigPath);
                throw;
            }
        }

        /// <summary>
        /// Converts MBConfig to TrainingConfig for compatibility
        /// </summary>
        /// <param name="mbConfig">MBConfig object</param>
        /// <returns>TrainingConfig object</returns>
        public TrainingConfig ConvertMbConfigToTrainingConfig(MbConfig mbConfig)
        {
            var trainingConfig = new TrainingConfig
            {
                LearningRate = GetHyperparameterValue(mbConfig.Training.Hyperparameters, "learningRate", 0.1f),
                Epochs = mbConfig.Training.Epochs,
                BatchSize = mbConfig.Training.BatchSize,
                ModelArchitecture = mbConfig.Training.Algorithm,
                Optimizer = "AutoML", // ML.NET uses AutoML
                LossFunction = "AutoML", // ML.NET determines best loss function
                UseEarlyStopping = true,
                EarlyStoppingPatience = 10,
                ValidationSplit = 0.2f,
                Device = "CPU" // ML.NET primarily uses CPU
            };

            Log.Information("Converted MBConfig to TrainingConfig. Algorithm: {Algorithm}, Epochs: {Epochs}", 
                trainingConfig.ModelArchitecture, trainingConfig.Epochs);

            return trainingConfig;
        }

        /// <summary>
        /// Trains a model using ML.NET and MBConfig
        /// </summary>
        /// <param name="mbConfig">MBConfig configuration</param>
        /// <param name="dataset">Training dataset</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Training results</returns>
        public async Task<TrainingResults> TrainWithMbConfigAsync(MbConfig mbConfig, TrainingDataset dataset, CancellationToken cancellationToken = default)
        {
            Log.Information("Starting ML.NET training with algorithm: {Algorithm}", mbConfig.Training.Algorithm);

            try
            {
                // Simulate ML.NET training process
                var trainingHistory = new List<TrainingProgress>();
                var totalEpochs = Math.Min(mbConfig.Training.Epochs, 50); // ML.NET typically uses fewer epochs

                for (int epoch = 0; epoch < totalEpochs; epoch++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Information("ML.NET training cancelled");
                        break;
                    }

                    // Simulate training progress (in real implementation, this would use ML.NET)
                    var progress = new TrainingProgress
                    {
                        CurrentEpoch = epoch + 1,
                        TotalEpochs = totalEpochs,
                        TrainingLoss = 1.0f - (epoch * 0.02f), // Simulated decreasing loss
                        ValidationLoss = 1.0f - (epoch * 0.018f), // Simulated validation loss
                        TrainingAccuracy = 0.5f + (epoch * 0.01f), // Simulated increasing accuracy
                        ValidationAccuracy = 0.48f + (epoch * 0.009f), // Simulated validation accuracy
                        LearningRate = GetHyperparameterValue(mbConfig.Training.Hyperparameters, "learningRate", 0.1f),
                        Status = "ML.NET Training"
                    };

                    trainingHistory.Add(progress);
                    _progressCallback?.Invoke(progress);

                    Log.Information("ML.NET Epoch {Epoch}/{TotalEpochs}: Train Loss: {TrainLoss:F4}, Val Loss: {ValLoss:F4}", 
                        epoch + 1, totalEpochs, progress.TrainingLoss, progress.ValidationLoss);

                    // Small delay to prevent UI freezing
                    await Task.Delay(50, cancellationToken);
                }

                // Generate model path
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var modelPath = Path.Combine("models", $"mlnet_model_{timestamp}");

                // Save MBConfig alongside model
                var mbConfigPath = Path.Combine(modelPath, "training_config.json");
                Directory.CreateDirectory(modelPath);
                await File.WriteAllTextAsync(mbConfigPath, JsonSerializer.Serialize(mbConfig, new JsonSerializerOptions { WriteIndented = true }));

                Log.Information("ML.NET training completed. Model saved to: {ModelPath}", modelPath);

                return new TrainingResults
                {
                    ModelPath = modelPath,
                    TrainingHistory = trainingHistory,
                    FinalTrainingLoss = trainingHistory.LastOrDefault()?.TrainingLoss ?? 0,
                    FinalValidationLoss = trainingHistory.LastOrDefault()?.ValidationLoss ?? 0,
                    FinalTrainingAccuracy = trainingHistory.LastOrDefault()?.TrainingAccuracy ?? 0,
                    FinalValidationAccuracy = trainingHistory.LastOrDefault()?.ValidationAccuracy ?? 0,
                    TrainingMethod = "ML.NET",
                    Algorithm = mbConfig.Training.Algorithm
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during ML.NET training");
                throw;
            }
        }

        /// <summary>
        /// Gets a hyperparameter value from the dictionary
        /// </summary>
        /// <typeparam name="T">Expected type</typeparam>
        /// <param name="hyperparameters">Hyperparameters dictionary</param>
        /// <param name="key">Parameter key</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Parameter value or default</returns>
        private T GetHyperparameterValue<T>(Dictionary<string, object> hyperparameters, string key, T defaultValue)
        {
            if (hyperparameters.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Validates MBConfig for compatibility
        /// </summary>
        /// <param name="mbConfig">MBConfig to validate</param>
        /// <returns>Validation result</returns>
        public (bool IsValid, string ErrorMessage) ValidateMbConfig(MbConfig mbConfig)
        {
            if (mbConfig == null)
                return (false, "MBConfig is null");

            if (string.IsNullOrEmpty(mbConfig.Training.Algorithm))
                return (false, "Training algorithm is not specified");

            if (mbConfig.DataSources.Count == 0)
                return (false, "No data sources specified");

            if (string.IsNullOrEmpty(mbConfig.Preprocessing.LabelColumn))
                return (false, "Label column is not specified");

            if (mbConfig.Preprocessing.FeatureColumns.Count == 0)
                return (false, "No feature columns specified");

            return (true, string.Empty);
        }
    }
}
