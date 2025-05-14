// DaminionOllamaInteractionLib/DaminionApiClient.cs
// This is the state AFTER applying "Step 2.A" (enhancing GetTagsAsync).

using System;
using System.Collections.Generic; // Required for List
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using DaminionOllamaInteractionLib.Daminion; // <--- CRUCIAL: Using directive for DTOs

namespace DaminionOllamaInteractionLib
{
    public class DaminionApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string? _apiBaseUrl;
        private string? _authenticationCookie;

        public DaminionApiClient()
        {
            _httpClient = new HttpClient(); // IDE0017 suggestion: can be simplified to `= new();`
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_authenticationCookie);

        public async Task<bool> LoginAsync(string daminionServerUrl, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(daminionServerUrl))
                throw new ArgumentException("Daminion server URL cannot be empty.", nameof(daminionServerUrl));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            _apiBaseUrl = daminionServerUrl.TrimEnd('/');
            string loginUrl = $"{_apiBaseUrl}/account/login";

            // IDE0017 suggestion: new LoginRequest can be simplified to new()
            var loginRequest = new LoginRequest { UsernameOrEmailAddress = username, Password = password };
            string jsonRequest = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                _httpClient.DefaultRequestHeaders.Remove("Cookie");

                HttpResponseMessage response = await _httpClient.PostAsync(loginUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    if (response.Headers.TryGetValues("Set-Cookie", out var cookieValues))
                    {
                        _authenticationCookie = cookieValues.FirstOrDefault(c => c.StartsWith("AspNet.ApplicationCookie=", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(_authenticationCookie))
                        {
                            _authenticationCookie = _authenticationCookie.Split(';')[0];
                            _httpClient.DefaultRequestHeaders.Add("Cookie", _authenticationCookie);
                            return true;
                        }
                        else
                        {
                            Console.Error.WriteLine("Login successful but authentication cookie was not found in the response.");
                            _authenticationCookie = null;
                            return false;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Login successful but 'Set-Cookie' header was not found in the response.");
                        _authenticationCookie = null;
                        return false;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Login failed. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}, Content: {errorContent}");
                    _authenticationCookie = null;
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"HTTP request error during login: {ex.Message}");
                _authenticationCookie = null;
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An unexpected error occurred during login: {ex.Message}");
                _authenticationCookie = null;
                throw;
            }
        }

        /// <summary>
        /// Gets a list of tags from the Daminion server.
        /// </summary>
        /// <returns>A list of DaminionTag objects or null if an error occurs.</returns>
        public async Task<List<DaminionTag>?> GetTagsAsync() // Line 104 (approx) in your file for the error
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("Cannot get tags: Client is not authenticated or API base URL is not set.");
                return null;
            }

            string tagsUrl = $"{_apiBaseUrl}/api/settings/getTags";
            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(tagsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Line 123 (approx) in your file for the error
                    var getTagsResponse = JsonSerializer.Deserialize<DaminionGetTagsResponse>(responseBody);
                    if (getTagsResponse != null && getTagsResponse.Success)
                    {
                        return getTagsResponse.Data;
                    }
                    else
                    {
                        // IDE0057 suggestion: Substring can be simplified using ranges, e.g., responseBody[..Math.Min(responseBody.Length, 500)]
                        Console.Error.WriteLine($"Failed to get tags. Success: {getTagsResponse?.Success}, Error: {getTagsResponse?.Error}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return null;
                    }
                }
                else
                {
                    // IDE0057 suggestion: Substring can be simplified
                    Console.Error.WriteLine($"Failed to get tags. Status: {response.StatusCode}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return null;
                }
            }
            catch (JsonException jsonEx)
            {
                Console.Error.WriteLine($"Error deserializing tags response: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting tags: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    // This LoginRequest DTO should also be in your library,
    // either directly in this namespace or in DaminionOllamaInteractionLib.Daminion
    // For simplicity here, keeping it as it was in the previous step.
    // If you moved it to the .Daminion sub-namespace, ensure this client can find it or move it back.
    internal class LoginRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("username0rEmailAddress")]
        public string UsernameOrEmailAddress { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}