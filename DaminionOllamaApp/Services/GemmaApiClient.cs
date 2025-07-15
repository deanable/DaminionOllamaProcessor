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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;

namespace DaminionOllamaApp.Services
{
    public class GemmaApiClient
    {
        private readonly string _modelName;
        private readonly string _serviceAccountJsonPath;
        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private DateTime _accessTokenExpiry;
        // Use the correct Gemini/Gemma API scope
        private static readonly string[] Scopes = new[] { "https://www.googleapis.com/auth/generative-language" };

        public GemmaApiClient(string serviceAccountJsonPath, string modelName)
        {
            _serviceAccountJsonPath = serviceAccountJsonPath;
            _modelName = modelName;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiry)
            {
                return _accessToken;
            }
            GoogleCredential credential;
            using (var stream = new FileStream(_serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }
            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            // Token expiry is not directly available, so set a conservative expiry (50 min)
            _accessToken = token;
            _accessTokenExpiry = DateTime.UtcNow.AddMinutes(50);
            return _accessToken;
        }

        public async Task<string> GenerateContentAsync(string prompt)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var url = "https://generativelanguage.googleapis.com/v1beta/models";
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Centralized logging: log the raw response body to the main app log
                if (App.Logger != null)
                {
                    App.Logger.Log($"[Gemma] Raw response from {url}: {responseBody}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (App.Logger != null)
                    {
                        App.Logger.Log($"[Gemma] ListModelsAsync failed. Status: {response.StatusCode}, Body: {responseBody}");
                    }
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
                if (App.Logger != null)
                {
                    App.Logger.Log($"[Gemma] Exception in ListModelsAsync: {ex.Message}\n{ex}");
                }
            }
            return models;
        }
    }
} 