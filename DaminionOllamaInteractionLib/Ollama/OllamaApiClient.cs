// DaminionOllamaInteractionLib/Ollama/OllamaApiClient.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json; // Required for JsonException and JsonSerializer
using System.Threading.Tasks;

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Client for interacting with the Ollama API.
    /// </summary>
    public class OllamaApiClient : IDisposable
    {
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

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}