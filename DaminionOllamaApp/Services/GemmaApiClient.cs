using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
    }
} 