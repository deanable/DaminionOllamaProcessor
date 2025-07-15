using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using System.IO;

namespace DaminionOllamaApp.Services
{
    public class GemmaApiClient
    {
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly HttpClient _httpClient;

        public GemmaApiClient(string apiKey, string modelName)
        {
            _apiKey = apiKey;
            _modelName = modelName;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GenerateContentAsync(string prompt)
        {
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemma API error: {response.StatusCode} - {responseBody}");
            }

            return responseBody;
        }

        public async Task<List<string>> ListModelsAsync()
        {
            var models = new List<string>();
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}";
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Log to gemmaapiclient.log
                var logDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "DaminionOllamaApp", "logs");
                Directory.CreateDirectory(logDir);
                var gemmaLogPath = Path.Combine(logDir, "gemmaapiclient.log");
                var mainLogPath = Directory.GetFiles(logDir, "log-*.txt").OrderByDescending(f => f).FirstOrDefault();
                var logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(gemmaLogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                    .CreateLogger();
                logger.Information("[GemmaApiClient] Raw response from {Url}: {ResponseBody}", url, responseBody);

                // Also append to the main log file if it exists
                if (!string.IsNullOrEmpty(mainLogPath))
                {
                    try
                    {
                        File.AppendAllText(mainLogPath, $"[GemmaApiClient] Raw response from {url}: {responseBody}{Environment.NewLine}");
                    }
                    catch { /* Ignore errors writing to main log */ }
                }

                if (!response.IsSuccessStatusCode)
                {
                    return models;
                }
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("models", out var modelsElement))
                {
                    foreach (var model in modelsElement.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                models.Add(name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GemmaApiClient] Exception in ListModelsAsync: {Message}", ex.Message);
            }
            return models;
        }
    }
} 