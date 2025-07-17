// DaminionOllamaApp/Services/SettingsService.cs
using DaminionOllamaApp.Models;
using System;
using System.IO;
using System.Text.Json;
using DaminionOllamaApp.Services; // Add this for App.Logger
using System.Collections.Generic; // Add this for Dictionary

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Service for loading and saving application settings to disk as JSON.
    /// Handles file I/O and error logging for settings persistence.
    /// </summary>
    public class SettingsService
    {
        private static readonly string AppName = "DaminionOllamaApp";
        private static readonly string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            // Store settings in a subdirectory within the user's LocalApplicationData folder
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appSpecificFolder = Path.Combine(appDataPath, AppName);
            Directory.CreateDirectory(appSpecificFolder); // Ensure the directory exists
            _settingsFilePath = Path.Combine(appSpecificFolder, SettingsFileName);
        }

        /// <summary>
        /// Loads application settings from the settings file. Returns default settings if the file does not exist or is invalid.
        /// </summary>
        /// <returns>The loaded <see cref="AppSettings"/> instance.</returns>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings(); // Return new settings if deserialization fails
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception (e.g., corrupted settings file)
                if (App.Logger != null)
                {
                    App.Logger.Log($"Error loading settings: {ex.Message}");
                }
                else
                {
                    Console.Error.WriteLine($"Error loading settings: {ex.Message}");
                }
            }
            return new AppSettings(); // Return default settings if file doesn't exist or error occurs
        }

        /// <summary>
        /// Saves the given application settings to disk as JSON.
        /// </summary>
        /// <param name="settings">The <see cref="AppSettings"/> instance to save.</param>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle the exception (e.g., permission issues)
                if (App.Logger != null)
                {
                    App.Logger.Log($"Error saving settings: {ex.Message}");
                }
                else
                {
                    Console.Error.WriteLine($"Error saving settings: {ex.Message}");
                }
                // Optionally, re-throw or notify the user
            }
        }
    }

    /// <summary>
    /// Static table containing pricing and free tier information for supported AI models.
    /// </summary>
    public static class ModelPricingTable
    {
        // Example: Update with real values as needed
        public static readonly Dictionary<string, ModelPricingInfo> Pricing = new Dictionary<string, ModelPricingInfo>
        {
            // Gemini 1.5 Pro
            { "gemini-1.5-pro", new ModelPricingInfo { PricePer1KInputTokens = 0.005, PricePer1KOutputTokens = 0.015, FreeInputTokens = 1500000, FreeOutputTokens = 0 } },
            // Gemini 1.5 Flash
            { "gemini-1.5-flash", new ModelPricingInfo { PricePer1KInputTokens = 0.003, PricePer1KOutputTokens = 0.009, FreeInputTokens = 5000000, FreeOutputTokens = 0 } },
            // Gemma (always free)
            { "gemma-2-9b-it", new ModelPricingInfo { PricePer1KInputTokens = 0, PricePer1KOutputTokens = 0, FreeInputTokens = int.MaxValue, FreeOutputTokens = int.MaxValue } },
            { "gemma-2-27b-it", new ModelPricingInfo { PricePer1KInputTokens = 0, PricePer1KOutputTokens = 0, FreeInputTokens = int.MaxValue, FreeOutputTokens = int.MaxValue } },
        };
    }

    /// <summary>
    /// Represents pricing information for a single AI model.
    /// </summary>
    public class ModelPricingInfo
    {
        /// <summary>Price per 1,000 input tokens (USD).</summary>
        public double PricePer1KInputTokens { get; set; }
        /// <summary>Price per 1,000 output tokens (USD).</summary>
        public double PricePer1KOutputTokens { get; set; }
        /// <summary>Number of free input tokens available.</summary>
        public int FreeInputTokens { get; set; }
        /// <summary>Number of free output tokens available.</summary>
        public int FreeOutputTokens { get; set; }
    }
}