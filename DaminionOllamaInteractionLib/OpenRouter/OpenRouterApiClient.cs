// DaminionOllamaInteractionLib/OpenRouter/OpenRouterApiClient.cs
using DaminionOllamaInteractionLib.Ollama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DaminionOllamaInteractionLib.OpenRouter
{
    /// <summary>
    /// Client for interacting with the OpenRouter.ai API.
    /// </summary>
    public class OpenRouterApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterApiClient"/> class.
        /// </summary>
        /// <param name="apiKey">The API key for OpenRouter.</param>
        /// <param name="httpReferer">The HTTP referer, required by OpenRouter.</param>
        public OpenRouterApiClient(string apiKey, string httpReferer)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("OpenRouter API key cannot be empty.", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            if (!string.IsNullOrWhiteSpace(httpReferer))
            {
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", httpReferer);
            }
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromMinutes(5);

            Console.WriteLine("[OpenRouterApiClient] Initialized.");
        }

        /// <summary>
        /// Lists the available models from the OpenRouter API.
        /// </summary>
        /// <returns>A response object containing the list of models.</returns>
        public async Task<OpenRouterListModelsResponse?> ListModelsAsync()
        {
            try
            {
                Console.WriteLine("[OpenRouterApiClient] Fetching models from /models endpoint.");
                HttpResponseMessage response = await _httpClient.GetAsync("models");
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[OpenRouterApiClient] Failed to fetch models. Status: {response.StatusCode}. Body: {responseBody}");
                    return null;
                }

                return JsonSerializer.Deserialize<OpenRouterListModelsResponse>(responseBody);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenRouterApiClient] Error fetching models: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes an image using a multimodal model on OpenRouter.
        /// </summary>
        /// <param name="modelName">The name of the model to use (e.g., "google/gemini-pro-vision").</param>
        /// <param name="prompt">The text prompt for the analysis.</param>
        /// <param name="base64Image">The base64 encoded image string.</param>
        /// <returns>The content of the AI's response.</returns>
        public async Task<string?> AnalyzeImageAsync(string modelName, string prompt, string base64Image)
        {
            var requestPayload = new
            {
                model = modelName,
                messages = new[]
                {
                    new {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } }
                        }
                    }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                Console.WriteLine($"[OpenRouterApiClient] Sending chat completion request to model: {modelName}");

                HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[OpenRouterApiClient] Chat completion request failed. Status: {response.StatusCode}");
                    Console.Error.WriteLine($"[OpenRouterApiClient] Response: {responseBody}");
                    
                    // Try to parse OpenRouter error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<OpenRouterErrorResponse>(responseBody);
                        return $"Error: {errorResponse?.Error?.Message ?? "Unknown API error"}";
                    }
                    catch
                    {
                        return $"Error: API request failed with status {response.StatusCode}. Response: {responseBody}";
                    }
                }

                var openRouterResponse = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseBody);
                return openRouterResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenRouterApiClient] Error during chat completion request: {ex.Message}");
                return $"Error: An exception occurred. {ex.Message}";
            }
        }

        /// <summary>
        /// Analyzes an image using a multimodal model on OpenRouter with proper image format detection.
        /// </summary>
        /// <param name="modelName">The name of the model to use (e.g., "google/gemini-pro-vision").</param>
        /// <param name="prompt">The text prompt for the analysis.</param>
        /// <param name="imageBytes">The image bytes.</param>
        /// <returns>The content of the AI's response.</returns>
        public async Task<string?> AnalyzeImageAsync(string modelName, string prompt, byte[] imageBytes)
        {
            string mimeType = GetImageMimeType(imageBytes);
            string base64Image = Convert.ToBase64String(imageBytes);
            
            var requestPayload = new
            {
                model = modelName,
                messages = new[]
                {
                    new {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                        }
                    }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                Console.WriteLine($"[OpenRouterApiClient] Sending request to model: {modelName}");
                Console.WriteLine($"[OpenRouterApiClient] Image type: {mimeType}, Size: {imageBytes.Length} bytes");

                HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[OpenRouterApiClient] Request failed. Status: {response.StatusCode}");
                    Console.Error.WriteLine($"[OpenRouterApiClient] Response: {responseBody}");
                    
                    // Try to parse OpenRouter error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<OpenRouterErrorResponse>(responseBody);
                        return $"Error: {errorResponse?.Error?.Message ?? "Unknown API error"}";
                    }
                    catch
                    {
                        return $"Error: API request failed with status {response.StatusCode}. Response: {responseBody}";
                    }
                }

                var openRouterResponse = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseBody);
                return openRouterResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenRouterApiClient] Exception: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if a model supports multimodal/vision capabilities.
        /// </summary>
        /// <param name="modelName">The model name to check.</param>
        /// <returns>True if the model supports vision, false otherwise.</returns>
        public async Task<bool> IsModelMultimodalAsync(string modelName)
        {
            var modelsResponse = await ListModelsAsync();
            if (modelsResponse?.Data == null) return false;
            
            var model = modelsResponse.Data.FirstOrDefault(m => m.Id == modelName);
            if (model == null) return false;
            
            // Check if model supports vision/multimodal
            return modelName.Contains("vision") || 
                   modelName.Contains("claude-3") || 
                   modelName.Contains("gpt-4") || 
                   modelName.Contains("gemini");
        }

        /// <summary>
        /// Detects the MIME type of an image from its byte signature.
        /// </summary>
        /// <param name="imageBytes">The image bytes.</param>
        /// <returns>The MIME type string.</returns>
        private static string GetImageMimeType(byte[] imageBytes)
        {
            if (imageBytes.Length >= 4)
            {
                // PNG signature
                if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                    return "image/png";
                
                // JPEG signature
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                    return "image/jpeg";
                
                // GIF signature
                if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                    return "image/gif";
                
                // WebP signature
                if (imageBytes.Length >= 12 && imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && 
                    imageBytes[2] == 0x46 && imageBytes[3] == 0x46 && imageBytes[8] == 0x57 && 
                    imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
                    return "image/webp";
            }
            
            return "image/jpeg"; // Default fallback
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    // DTOs for OpenRouter API responses

    public class OpenRouterListModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenRouterModel>? Data { get; set; }
    }

    public class OpenRouterModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class OpenRouterChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    public class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
    }

    public class OpenRouterMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class OpenRouterErrorResponse
    {
        [JsonPropertyName("error")]
        public OpenRouterError? Error { get; set; }
    }

    public class OpenRouterError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}