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
using Serilog;
using System.IO;
using Serilog.Sinks.File;

namespace DaminionOllamaInteractionLib.OpenRouter
{
    /// <summary>
    /// Client for interacting with the OpenRouter.ai API service.
    /// OpenRouter provides access to multiple AI models through a unified API interface.
    /// This client handles authentication, model listing, and image analysis requests.
    /// 
    /// The client supports:
    /// - Authentication with API keys
    /// - Model discovery and listing
    /// - Image analysis with various AI models
    /// - Base64 image encoding and processing
    /// </summary>
    public class OpenRouterApiClient : IDisposable
    {
        private static readonly ILogger Logger;
        static OpenRouterApiClient()
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaminionOllamaApp", "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "openrouterapiclient.log");
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
        }
        #region Private Fields
        /// <summary>
        /// HTTP client for making API requests to OpenRouter.
        /// </summary>
        private readonly HttpClient _httpClient;
        
        /// <summary>
        /// API key for authenticating with the OpenRouter service.
        /// </summary>
        private readonly string _apiKey;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the OpenRouterApiClient class.
        /// Sets up HTTP client with proper authentication headers and base URL.
        /// </summary>
        /// <param name="apiKey">The API key for OpenRouter authentication.</param>
        /// <param name="httpReferer">The HTTP referer header, required by OpenRouter for tracking.</param>
        /// <exception cref="ArgumentException">Thrown when the API key is null or empty.</exception>
        public OpenRouterApiClient(string apiKey, string httpReferer)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("OpenRouter API key cannot be empty.", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = new HttpClient();
            
            // Configure HTTP client for OpenRouter API
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            // Add HTTP referer if provided (required by OpenRouter)
            if (!string.IsNullOrWhiteSpace(httpReferer))
            {
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", httpReferer);
            }
            
            // Set content type and timeout
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Extended timeout for AI processing

            Console.WriteLine("[OpenRouterApiClient] Initialized.");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Retrieves the list of available AI models from the OpenRouter API.
        /// This can be used to discover which models are available for image analysis.
        /// </summary>
        /// <returns>A response object containing the list of available models, or null if the request fails.</returns>
        public async Task<OpenRouterListModelsResponse?> ListModelsAsync()
        {
            try
            {
                Console.WriteLine("[OpenRouterApiClient] Fetching available models...");
                
                var response = await _httpClient.GetAsync("models");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OpenRouterListModelsResponse>(json);
                    
                    Console.WriteLine($"[OpenRouterApiClient] Found {result?.Data?.Count ?? 0} models.");
                    return result;
                }
                else
                {
                    Console.Error.WriteLine($"[OpenRouterApiClient] Failed to fetch models: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenRouterApiClient] Error fetching models: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes an image using the specified AI model and prompt.
        /// Sends the image as base64 data to the OpenRouter API for processing.
        /// </summary>
        /// <param name="modelName">The name of the AI model to use for analysis (e.g., "openai/gpt-4-vision-preview").</param>
        /// <param name="prompt">The text prompt describing what analysis to perform on the image.</param>
        /// <param name="base64Image">The image data encoded as a base64 string.</param>
        /// <returns>The AI-generated analysis text, or null if the request fails.</returns>
        public async Task<string?> AnalyzeImageAsync(string modelName, string prompt, string base64Image)
        {
            try
            {
                Console.WriteLine($"[OpenRouterApiClient] Analyzing image with model: {modelName}");
                
                // Construct the chat completion request
                var requestData = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { 
                                    type = "image_url", 
                                    image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } 
                                }
                            }
                        }
                    }
                };

                // Serialize and send the request
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseJson);
                    
                    // Extract the content from the response
                    var analysisResult = result?.Choices?.FirstOrDefault()?.Message?.Content;
                    
                    Console.WriteLine($"[OpenRouterApiClient] Analysis completed. Length: {analysisResult?.Length ?? 0} characters");
                    return analysisResult;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"[OpenRouterApiClient] Analysis failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenRouterApiClient] Error analyzing image: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes an image using a multimodal model on OpenRouter (byte array version).
        /// Automatically detects the image format and converts to base64.
        /// </summary>
        /// <param name="modelName">The name of the model to use (e.g., "google/gemini-pro-vision").</param>
        /// <param name="prompt">The text prompt for the analysis.</param>
        /// <param name="imageBytes">The image bytes.</param>
        /// <returns>The content of the AI's response.</returns>
        public async Task<string?> AnalyzeImageAsync(string modelName, string prompt, byte[] imageBytes)
        {
            string base64Image = Convert.ToBase64String(imageBytes);
            return await AnalyzeImageAsync(modelName, prompt, base64Image);
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
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                    return "image/jpeg";
            }
            
            return "image/jpeg"; // Default fallback
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Disposes of the HTTP client and releases associated resources.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
        #endregion

        // Example: Log API requests and responses
        private void LogApiRequest(string endpoint, object? payload = null)
        {
            Logger.Information("OpenRouter API Request: {Endpoint}, Payload: {@Payload}", endpoint, payload);
        }
        private void LogApiResponse(string endpoint, object? response = null)
        {
            Logger.Information("OpenRouter API Response: {Endpoint}, Response: {@Response}", endpoint, response);
        }
        private void LogApiError(string endpoint, Exception ex)
        {
            Logger.Error(ex, "OpenRouter API Error: {Endpoint}", endpoint);
        }
    }

    #region Data Models
    /// <summary>
    /// Represents the response from the OpenRouter models list API endpoint.
    /// Contains the list of available AI models that can be used for processing.
    /// </summary>
    public class OpenRouterListModelsResponse
    {
        /// <summary>
        /// Gets or sets the list of available AI models.
        /// </summary>
        [JsonPropertyName("data")]
        public List<OpenRouterModel>? Data { get; set; }
    }

    /// <summary>
    /// Represents an individual AI model available through OpenRouter.
    /// Contains metadata about the model's capabilities and identification.
    /// </summary>
    public class OpenRouterModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the model (e.g., "openai/gpt-4-vision-preview").
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        /// <summary>
        /// Gets or sets the human-readable name of the model.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents the response from the OpenRouter chat completion API endpoint.
    /// Contains the AI-generated responses and metadata about the completion.
    /// </summary>
    public class OpenRouterChatCompletionResponse
    {
        /// <summary>
        /// Gets or sets the list of choice responses from the AI model.
        /// Typically contains one choice with the generated content.
        /// </summary>
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    /// <summary>
    /// Represents a single choice/response from the AI model.
    /// Contains the generated message content and metadata.
    /// </summary>
    public class OpenRouterChoice
    {
        /// <summary>
        /// Gets or sets the message content generated by the AI model.
        /// </summary>
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
    }

    /// <summary>
    /// Represents a message in the chat completion response.
    /// Contains the actual text content generated by the AI model.
    /// </summary>
    public class OpenRouterMessage
    {
        /// <summary>
        /// Gets or sets the text content of the AI-generated response.
        /// This contains the actual analysis results for image processing requests.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    /// <summary>
    /// Represents an error response from the OpenRouter API.
    /// Used for parsing detailed error information when requests fail.
    /// </summary>
    public class OpenRouterErrorResponse
    {
        /// <summary>
        /// Gets or sets the error details from the API response.
        /// </summary>
        [JsonPropertyName("error")]
        public OpenRouterError? Error { get; set; }
    }

    /// <summary>
    /// Represents detailed error information from the OpenRouter API.
    /// Contains specific error messages and error codes.
    /// </summary>
    public class OpenRouterError
    {
        /// <summary>
        /// Gets or sets the human-readable error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the error type classification.
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the specific error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
    #endregion
}