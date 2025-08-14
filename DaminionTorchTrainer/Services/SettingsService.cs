using System;
using Microsoft.Win32;
using System.IO;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for persisting application settings to the Windows registry
    /// </summary>
    public class SettingsService
    {
        private const string RegistryKeyPath = @"SOFTWARE\DaminionTorchTrainer";
        private const string DaminionUrlKey = "DaminionUrl";
        private const string DaminionUsernameKey = "DaminionUsername";
        private const string DaminionPasswordKey = "DaminionPassword";
        private const string SearchQueryKey = "SearchQuery";
        private const string SearchOperatorsKey = "SearchOperators";
        private const string MaxItemsKey = "MaxItems";
        private const string LearningRateKey = "LearningRate";
        private const string EpochsKey = "Epochs";
        private const string BatchSizeKey = "BatchSize";
        private const string DeviceKey = "Device";
        private const string OutputPathKey = "OutputPath";

        /// <summary>
        /// Loads settings from the registry
        /// </summary>
        /// <returns>Application settings</returns>
        public static AppSettings LoadSettings()
        {
            var settings = new AppSettings();

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        settings.DaminionUrl = key.GetValue(DaminionUrlKey)?.ToString() ?? "http://localhost:8080";
                        settings.DaminionUsername = key.GetValue(DaminionUsernameKey)?.ToString() ?? "";
                        settings.DaminionPassword = key.GetValue(DaminionPasswordKey)?.ToString() ?? "";
                        settings.SearchQuery = key.GetValue(SearchQueryKey)?.ToString() ?? "";
                        settings.SearchOperators = key.GetValue(SearchOperatorsKey)?.ToString() ?? "";
                        settings.MaxItems = int.TryParse(key.GetValue(MaxItemsKey)?.ToString(), out int maxItems) ? maxItems : 1000;
                        settings.LearningRate = float.TryParse(key.GetValue(LearningRateKey)?.ToString(), out float learningRate) ? learningRate : 0.001f;
                        settings.Epochs = int.TryParse(key.GetValue(EpochsKey)?.ToString(), out int epochs) ? epochs : 100;
                        settings.BatchSize = int.TryParse(key.GetValue(BatchSizeKey)?.ToString(), out int batchSize) ? batchSize : 32;
                        settings.Device = key.GetValue(DeviceKey)?.ToString() ?? "CPU";
                        settings.OutputPath = key.GetValue(OutputPathKey)?.ToString() ?? GetDefaultOutputPath();
                    }
                    else
                    {
                        // Set default values if registry key doesn't exist
                        settings.DaminionUrl = "http://localhost:8080";
                        settings.MaxItems = 1000;
                        settings.LearningRate = 0.001f;
                        settings.Epochs = 100;
                        settings.BatchSize = 32;
                        settings.Device = "CPU";
                        settings.OutputPath = GetDefaultOutputPath();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings from registry: {ex.Message}");
                // Return default settings if registry access fails
                settings.DaminionUrl = "http://localhost:8080";
                settings.MaxItems = 1000;
                settings.LearningRate = 0.001f;
                settings.Epochs = 100;
                settings.BatchSize = 32;
                settings.Device = "CPU";
                settings.OutputPath = GetDefaultOutputPath();
            }

            return settings;
        }

        /// <summary>
        /// Saves settings to the registry
        /// </summary>
        /// <param name="settings">Application settings to save</param>
        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(DaminionUrlKey, settings.DaminionUrl ?? "");
                        key.SetValue(DaminionUsernameKey, settings.DaminionUsername ?? "");
                        key.SetValue(DaminionPasswordKey, settings.DaminionPassword ?? "");
                        key.SetValue(SearchQueryKey, settings.SearchQuery ?? "");
                        key.SetValue(SearchOperatorsKey, settings.SearchOperators ?? "");
                        key.SetValue(MaxItemsKey, settings.MaxItems);
                        key.SetValue(LearningRateKey, settings.LearningRate);
                        key.SetValue(EpochsKey, settings.Epochs);
                        key.SetValue(BatchSizeKey, settings.BatchSize);
                        key.SetValue(DeviceKey, settings.Device ?? "CPU");
                        key.SetValue(OutputPathKey, settings.OutputPath ?? GetDefaultOutputPath());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings to registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default output path for models
        /// </summary>
        /// <returns>Default output path</returns>
        private static string GetDefaultOutputPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DaminionTorchTrainer", "Models");
        }
    }

    /// <summary>
    /// Application settings model
    /// </summary>
    public class AppSettings
    {
        public string DaminionUrl { get; set; } = "http://localhost:8080";
        public string DaminionUsername { get; set; } = "";
        public string DaminionPassword { get; set; } = "";
        public string SearchQuery { get; set; } = "";
        public string SearchOperators { get; set; } = "";
        public int MaxItems { get; set; } = 1000;
        public float LearningRate { get; set; } = 0.001f;
        public int Epochs { get; set; } = 100;
        public int BatchSize { get; set; } = 32;
        public string Device { get; set; } = "CPU";
        public string OutputPath { get; set; } = "";
    }
}
