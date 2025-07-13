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
    #endregion
}