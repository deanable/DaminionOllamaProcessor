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

        // Add these methods inside your public class DaminionApiClient : IDisposable

        // ... (after GetTagsAsync() method, for example) ...

        public async Task<DaminionPathResult> GetAbsolutePathsAsync(List<long> itemIds)
        {
            var result = new DaminionPathResult { Success = false, Paths = new Dictionary<string, string>() };
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                result.ErrorMessage = "Client is not authenticated or API base URL is not set.";
                return result;
            }
            if (itemIds == null || !itemIds.Any())
            {
                result.ErrorMessage = "Item IDs list cannot be null or empty.";
                return result;
            }

            string idsQueryParam = string.Join(",", itemIds);
            string pathsUrl = $"{_apiBaseUrl}/api/mediaItems/getAbsolutePaths?ids={idsQueryParam}";

            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(pathsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Dictionary<string, string>? paths = null;
                    try
                    {
                        paths = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
                    }
                    catch (JsonException)
                    {
                        try
                        {
                            var wrappedResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                            if (wrappedResponse.TryGetProperty("data", out var dataElement))
                            {
                                paths = JsonSerializer.Deserialize<Dictionary<string, string>>(dataElement.GetRawText());
                            }
                        }
                        catch (JsonException innerEx)
                        {
                            Console.Error.WriteLine($"Failed to deserialize paths (inner attempt): {innerEx.Message}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                            result.ErrorMessage = $"Failed to deserialize paths response: {innerEx.Message}";
                            return result;
                        }
                    }

                    if (paths != null)
                    {
                        result.Paths = paths;
                        result.Success = true;
                    }
                    else
                    {
                        result.ErrorMessage = "Successfully called API, but paths data was not in the expected format or was empty.";
                        Console.Error.WriteLine($"Paths data not in expected format. Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    }
                    return result;
                }
                else
                {
                    result.ErrorMessage = $"API call failed. Status: {response.StatusCode}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}";
                    Console.Error.WriteLine(result.ErrorMessage);
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error getting absolute paths: {ex.Message}";
                Console.Error.WriteLine(result.ErrorMessage);
                return result;
            }
        }

        public async Task<DaminionBatchChangeResponse?> UpdateItemMetadataAsync(List<long> itemIds, List<DaminionUpdateOperation> operations)
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("Cannot update metadata: Client is not authenticated or API base URL is not set.");
                return new DaminionBatchChangeResponse { Success = false, Error = "Not authenticated." };
            }
            if (itemIds == null || !itemIds.Any() || operations == null || !operations.Any())
            {
                Console.Error.WriteLine("Item IDs and operations list cannot be null or empty for updating metadata.");
                return new DaminionBatchChangeResponse { Success = false, Error = "Item IDs or operations missing." };
            }

            string updateUrl = $"{_apiBaseUrl}/api/itemData/batchChange";
            var requestPayload = new DaminionBatchChangeRequest
            {
                Ids = itemIds,
                Data = operations
            };

            string jsonRequest = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(updateUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                var batchChangeResponse = JsonSerializer.Deserialize<DaminionBatchChangeResponse>(responseBody);
                if (batchChangeResponse == null)
                {
                    if (response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(responseBody))
                    {
                        return new DaminionBatchChangeResponse { Success = true };
                    }
                    Console.Error.WriteLine($"Failed to deserialize batchChange response or response was empty. Status: {response.StatusCode}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return new DaminionBatchChangeResponse { Success = false, Error = $"Failed to deserialize response. Status: {response.StatusCode}" };
                }

                if (!batchChangeResponse.Success)
                {
                    Console.Error.WriteLine($"Batch change operation failed. Error: {batchChangeResponse.Error}, Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                }
                return batchChangeResponse;
            }
            catch (JsonException jsonEx) // This is where using System.Text.Json; is needed in this file.
            {
                Console.Error.WriteLine($"Error deserializing batchChange response: {jsonEx.Message}");
                return new DaminionBatchChangeResponse { Success = false, Error = $"JSON Deserialization error: {jsonEx.Message}" };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating item metadata: {ex.Message}");
                return new DaminionBatchChangeResponse { Success = false, Error = $"Exception: {ex.Message}" };
            }
        }

        // ... (ensure this is within the DaminionApiClient class, before the final closing brace `}`) ...


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
        [System.Text.Json.Serialization.JsonPropertyName("usernameOrEmailAddress")]
        public string UsernameOrEmailAddress { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}