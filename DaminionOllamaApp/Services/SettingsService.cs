// DaminionOllamaApp/Services/SettingsService.cs
using DaminionOllamaApp.Models;
using System;
using System.IO;
using System.Text.Json;
using DaminionOllamaApp.Services; // Add this for App.Logger
using System.Collections.Generic; // Add this for Dictionary

namespace DaminionOllamaApp.Services
{
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

    // Add this class to hold pricing and free tier info for supported models
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

    public class ModelPricingInfo
    {
        public double PricePer1KInputTokens { get; set; }
        public double PricePer1KOutputTokens { get; set; }
        public int FreeInputTokens { get; set; }
        public int FreeOutputTokens { get; set; }
    }
}