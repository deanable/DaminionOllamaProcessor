// DaminionOllamaApp/Services/SettingsService.cs
using DaminionOllamaApp.Models;
using System;
using System.IO;
using System.Text.Json;
using DaminionOllamaApp.Services; // Add this for App.Logger

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
}