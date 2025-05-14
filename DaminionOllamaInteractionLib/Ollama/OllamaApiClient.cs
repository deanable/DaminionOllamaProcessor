// DaminionOllamaInteractionLib/Ollama/OllamaApiClient.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DaminionOllamaInteractionLib.Ollama
{
    public class OllamaApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _apiBaseUrl; // Example: "http://localhost:11434"

        public OllamaApiClient(string ollamaServerUrl)
        {
            if (string.IsNullOrWhiteSpace(ollamaServerUrl))
                throw new ArgumentException("Ollama server URL cannot be empty.", nameof(ollamaServerUrl));

            _apiBaseUrl = ollamaServerUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Image processing can take time
        }

        /// <summary>
        /// Analyzes an image using the specified Llava model and prompt.
        /// </summary>
        /// <param name="modelName">The name of the Ollama model to use (e.g., "llava", "llava:7b").</param>
        /// <param name="prompt">The prompt to guide the image analysis.</param>
        /// <param name="imageBytes">The byte array of the image to analyze.</param>
        /// <returns>The textual response from the Ollama model, or null if an error occurs.</returns>
        /// <exception cref="ArgumentNullException">Thrown if modelName, prompt, or imageBytes are null/empty.</exception>
        /// <exception cref="HttpRequestException">Thrown on network errors.</exception>
        /// <exception cref="JsonException">Thrown on errors deserializing the Ollama response.</exception>
        /// <exception cref="Exception">Thrown for other unexpected errors.</exception>
        public async Task<OllamaGenerateResponse?> AnalyzeImageAsync(string modelName, string prompt, byte[] imageBytes)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentNullException(nameof(modelName), "Model name cannot be empty.");
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentNullException(nameof(prompt), "Prompt cannot be empty.");
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentNullException(nameof(imageBytes), "Image bytes cannot be null or empty.");

            string generateUrl = $"{_apiBaseUrl}/api/generate";
            string base64Image = Convert.ToBase64String(imageBytes);

            var requestPayload = new OllamaGenerateRequest
            {
                Model = modelName,
                Prompt = prompt,
                Images = new List<string> { base64Image },
                Stream = false // We want the full response
            };

            try
            {
                string jsonRequest = JsonSerializer.Serialize(requestPayload,
                    new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Console.WriteLine($"Ollama Request URL: {generateUrl}");
                // Console.WriteLine($"Ollama Request Payload: {jsonRequest}"); // Be careful logging full base64 images

                HttpResponseMessage response = await _httpClient.PostAsync(generateUrl, content);

                string responseBody = await response.Content.ReadAsStringAsync();
                // Console.WriteLine($"Ollama Response Status: {response.StatusCode}");
                // Console.WriteLine($"Ollama Response Body: {responseBody}");


                if (response.IsSuccessStatusCode)
                {
                    var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseBody);
                    if (ollamaResponse == null || string.IsNullOrEmpty(ollamaResponse.Response))
                    {
                        Console.Error.WriteLine("Ollama API returned success status but the response content is missing or invalid.");
                        // You might want to return the raw responseBody here or a specific error object
                        // For now, returning null or an empty response object to indicate partial failure.
                        return new OllamaGenerateResponse { Model = modelName, Response = $"Error: Successful API call but empty or invalid response body. Raw: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}", Done = false };
                    }
                    return ollamaResponse;
                }
                else
                {
                    Console.Error.WriteLine($"Ollama API request failed. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}, Body: {responseBody}");
                    // Construct a "failed" response object to carry the error message
                    return new OllamaGenerateResponse { Model = modelName, Response = $"Error: {response.StatusCode} - {response.ReasonPhrase}. Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}", Done = false };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"HTTP request error to Ollama: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error deserializing Ollama response: {ex.Message}");
                throw; // Or return an error-indicating response
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An unexpected error occurred while communicating with Ollama: {ex.Message}");
                throw; // Or return an error-indicating response
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
