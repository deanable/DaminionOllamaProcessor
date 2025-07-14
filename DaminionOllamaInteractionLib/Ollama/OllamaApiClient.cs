// DaminionOllamaInteractionLib/Ollama/OllamaApiClient.cs
using System;
using System.Collections.Generic; // For List in OllamaModelInfo if used directly
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json; // For JsonSerializer
using System.Threading.Tasks;
// Ensure this using statement correctly points to where your Ollama DTOs are:
using DaminionOllamaInteractionLib.Ollama;
using Serilog;
using System.IO;
using Serilog.Sinks.File;

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Client for interacting with the Ollama API.
    /// </summary>
    public class OllamaApiClient : IDisposable
    {
        private static readonly ILogger Logger;
        static OllamaApiClient()
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaminionOllamaApp", "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "ollamaapiclient.log");
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
        }

        private readonly HttpClient _httpClient;
        private string _apiBaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaApiClient"/> class.
        /// </summary>
        /// <param name="ollamaServerUrl"></param>
        /// <exception cref="ArgumentException"></exception>
        public OllamaApiClient(string ollamaServerUrl)
        {
            if (string.IsNullOrWhiteSpace(ollamaServerUrl))
                throw new ArgumentException("Ollama server URL cannot be empty.", nameof(ollamaServerUrl));

            _apiBaseUrl = ollamaServerUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Increased timeout for potentially long-running Ollama requests
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            Console.WriteLine($"[OllamaApiClient] Initialized with base URL: {_apiBaseUrl}");
        }

        /// <summary>
        /// Sends a request to the Ollama API to analyze an image with a given prompt.
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="prompt"></param>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<OllamaGenerateResponse?> AnalyzeImageAsync(string modelName, string prompt, byte[] imageBytes)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentNullException(nameof(modelName), "Model name cannot be empty.");
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentNullException(nameof(prompt), "Prompt cannot be empty.");
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentNullException(nameof(imageBytes), "Image bytes cannot be null or empty.");

            string generateUrl = $"{_apiBaseUrl}/api/generate";
            Console.WriteLine($"[OllamaApiClient] Attempting to analyze image. URL: {generateUrl}, Model: {modelName}");

            string base64Image = Convert.ToBase64String(imageBytes);
            var requestPayload = new OllamaGenerateRequest
            {
                Model = modelName,
                Prompt = prompt,
                Images = new List<string> { base64Image },
                Stream = false
            };

            try
            {
                string jsonRequest = JsonSerializer.Serialize(requestPayload,
                    new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Log a snippet of the request for brevity, as Base64 images are large.
                Console.WriteLine($"[OllamaApiClient] Request Payload (snippet): {{ \"model\": \"{modelName}\", \"prompt\": \"{prompt.Substring(0, Math.Min(prompt.Length, 50))}...\", \"images\": [\"Base64ImageSnippet...\"] }}");

                Console.WriteLine($"[OllamaApiClient] Sending POST request to {generateUrl}...");
                HttpResponseMessage response = await _httpClient.PostAsync(generateUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[OllamaApiClient] Response Status Code: {response.StatusCode}");
                Console.WriteLine($"[OllamaApiClient] Response Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");

                if (response.IsSuccessStatusCode)
                {
                    OllamaGenerateResponse? ollamaResponse = null;
                    try
                    {
                        ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseBody);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[OllamaApiClient] Error deserializing successful Ollama response: {jsonEx.Message}. StackTrace: {jsonEx.StackTrace}. Body: {responseBody}");
                        return new OllamaGenerateResponse { Model = modelName, Response = $"Error: Failed to parse successful response. {jsonEx.Message}", Done = false };
                    }

                    if (ollamaResponse == null || (string.IsNullOrEmpty(ollamaResponse.Response) && ollamaResponse.Done))
                    {
                        Console.Error.WriteLine("[OllamaApiClient] Ollama API returned success status but the response content is missing, invalid, or indicates an issue.");
                        return new OllamaGenerateResponse { Model = modelName, Response = $"Error: Successful API call but problematic response body. Raw: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}", Done = ollamaResponse?.Done ?? false };
                    }
                    Console.WriteLine("[OllamaApiClient] Successfully deserialized Ollama response.");
                    return ollamaResponse;
                }
                else
                {
                    Console.Error.WriteLine($"[OllamaApiClient] Ollama API request failed. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}.");
                    // The responseBody is already logged above.
                    return new OllamaGenerateResponse { Model = modelName, Response = $"Error: {response.StatusCode} - {response.ReasonPhrase}. See debug output for full body.", Done = false };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[OllamaApiClient] HTTP request error to Ollama: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"[OllamaApiClient] Inner Exception: {ex.InnerException.Message}");
                }
                Console.Error.WriteLine($"[OllamaApiClient] StackTrace: {ex.StackTrace}");
                return new OllamaGenerateResponse { Model = modelName, Response = $"Error: HTTP request failed. {ex.Message}", Done = false };
            }
            catch (System.Text.Json.JsonException ex) // For errors during request serialization
            {
                Console.Error.WriteLine($"[OllamaApiClient] Error serializing Ollama request: {ex.Message}");
                Console.Error.WriteLine($"[OllamaApiClient] StackTrace: {ex.StackTrace}");
                return new OllamaGenerateResponse { Model = modelName, Response = $"Error: JSON processing for request failed. {ex.Message}", Done = false };
            }
            catch (TaskCanceledException ex) // Often indicates a timeout
            {
                Console.Error.WriteLine($"[OllamaApiClient] Ollama request timed out: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"[OllamaApiClient] Inner Exception (Timeout): {ex.InnerException.Message}");
                }
                Console.Error.WriteLine($"[OllamaApiClient] StackTrace: {ex.StackTrace}");
                return new OllamaGenerateResponse { Model = modelName, Response = $"Error: Request to Ollama timed out. Timeout is {_httpClient.Timeout.TotalSeconds}s. {ex.Message}", Done = false };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OllamaApiClient] An unexpected error occurred: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"[OllamaApiClient] Inner Exception: {ex.InnerException.Message}");
                }
                Console.Error.WriteLine($"[OllamaApiClient] StackTrace: {ex.StackTrace}");
                return new OllamaGenerateResponse { Model = modelName, Response = $"Error: An unexpected error occurred. {ex.Message}", Done = false };
            }
        }

        // These methods go INSIDE the OllamaApiClient class

        /// <summary>
        /// Checks if the Ollama server is running and reachable.
        /// </summary>
        /// <returns>True if the server responds positively, false otherwise.</returns>
        public async Task<bool> CheckConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                Console.Error.WriteLine("[OllamaApiClient] CheckConnection Error: API base URL is not set.");
                return false;
            }

            string healthCheckUrl = _apiBaseUrl;
            Console.WriteLine($"[OllamaApiClient] Checking Ollama connection at: {healthCheckUrl}");

            try
            {
                // Use a temporary HttpClient for a quick check with a shorter timeout
                // Or, if _httpClient is already initialized with a suitable default timeout, you could use it.
                // Creating a new one here ensures a specific short timeout for this check.
                using (var tempHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }) // Increased timeout slightly
                {
                    HttpResponseMessage response = await tempHttpClient.GetAsync(healthCheckUrl);
                    Console.WriteLine($"[OllamaApiClient] Connection check response status: {response.StatusCode}");
                    // Optional: Check response body if needed, e.g., response.Content.ReadAsStringAsync();
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OllamaApiClient] Error checking Ollama connection to '{healthCheckUrl}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lists local models available on the Ollama server using the /api/tags endpoint.
        /// </summary>
        /// <returns>An OllamaListTagsResponse containing the list of models, or null if an error occurs.</returns>
        public async Task<OllamaListTagsResponse?> ListLocalModelsAsync()
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                Console.Error.WriteLine("[OllamaApiClient] ListLocalModels Error: API base URL is not set.");
                return null;
            }

            string listModelsUrl = $"{_apiBaseUrl}/api/tags";
            Console.WriteLine($"[OllamaApiClient] Listing Ollama models from: {listModelsUrl}");

            try
            {
                // Use the class member _httpClient, assuming its timeout is appropriate for this call.
                // If not, you might consider adjusting _httpClient.Timeout or using a temporary client like in CheckConnectionAsync.
                HttpResponseMessage response = await _httpClient.GetAsync(listModelsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OllamaApiClient] ListLocalModels Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    OllamaListTagsResponse? listResponse = null;
                    try
                    {
                        // Ensure System.Text.Json.JsonSerializer is used. Add 'using System.Text.Json;' if missing.
                        listResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaListTagsResponse>(responseBody,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[OllamaApiClient] Error deserializing ListLocalModels response: {jsonEx.Message}. Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return null;
                    }

                    if (listResponse != null)
                    {
                        Console.WriteLine($"[OllamaApiClient] Successfully fetched {listResponse.Models?.Count ?? 0} local models.");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[OllamaApiClient] ListLocalModels deserialization resulted in null object. Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    }
                    return listResponse;
                }
                else
                {
                    Console.Error.WriteLine($"[OllamaApiClient] ListLocalModels HTTP call failed. Status: {response.StatusCode}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return null;
                }
            }
            catch (Exception ex) // Catch general exceptions including HttpRequestException, TaskCanceledException (timeout)
            {
                Console.Error.WriteLine($"[OllamaApiClient] An unexpected error occurred during ListLocalModels from '{listModelsUrl}': {ex.Message}");
                return null;
            }
        }

        // Example: Log API requests and responses
        private void LogApiRequest(string endpoint, object? payload = null)
        {
            Logger.Information("Ollama API Request: {Endpoint}, Payload: {@Payload}", endpoint, payload);
        }
        private void LogApiResponse(string endpoint, object? response = null)
        {
            Logger.Information("Ollama API Response: {Endpoint}, Response: {@Response}", endpoint, response);
        }
        private void LogApiError(string endpoint, Exception ex)
        {
            Logger.Error(ex, "Ollama API Error: {Endpoint}", endpoint);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}