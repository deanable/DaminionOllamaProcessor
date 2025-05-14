// DaminionOllamaInteractionLib/DaminionApiClient.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json; // Required for JsonSerializer and JsonException
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DaminionOllamaInteractionLib.Daminion; // For Daminion DTOs

namespace DaminionOllamaInteractionLib
{
    public class DaminionApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string? _apiBaseUrl;
        private string? _authenticationCookie;

        public DaminionApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            Console.WriteLine("[DaminionApiClient] Initialized.");
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_authenticationCookie);

        public async Task<bool> LoginAsync(string daminionServerUrl, string username, string password)
        {
            Console.WriteLine("[DaminionApiClient] Attempting LoginAsync...");

            if (string.IsNullOrWhiteSpace(daminionServerUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] Login Error: Daminion server URL cannot be empty.");
                throw new ArgumentException("Daminion server URL cannot be empty.", nameof(daminionServerUrl));
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.Error.WriteLine("[DaminionApiClient] Login Error: Username cannot be empty.");
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            }
            // Password can be empty if Daminion allows it.

            _apiBaseUrl = daminionServerUrl.TrimEnd('/');
            string loginUrl = $"{_apiBaseUrl}/account/login";
            Console.WriteLine($"[DaminionApiClient] Login URL: {loginUrl}");

            var loginRequest = new LoginRequest { UsernameOrEmailAddress = username, Password = password };
            string jsonRequest = "";
            try
            {
                jsonRequest = JsonSerializer.Serialize(loginRequest);
                // WARNING: Logging passwords is a security risk. For temporary debugging only.
                Console.WriteLine($"[DaminionApiClient] Login Request Payload (JSON): {jsonRequest} <-- CONTAINS SENSITIVE DATA");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] Error serializing login request: {ex.Message}");
                throw;
            }

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _authenticationCookie = null;

                Console.WriteLine($"[DaminionApiClient] Sending POST request to {loginUrl}...");
                HttpResponseMessage response = await _httpClient.PostAsync(loginUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[DaminionApiClient] Login Response Status Code: {response.StatusCode} ({response.ReasonPhrase})");
                Console.WriteLine($"[DaminionApiClient] Login Response Headers: {response.Headers.ToString().Trim()}");
                Console.WriteLine($"[DaminionApiClient] Login Response Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 1000))}");

                if (response.IsSuccessStatusCode)
                {
                    if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookieValues))
                    {
                        _authenticationCookie = cookieValues.FirstOrDefault(c => c.StartsWith("AspNet.ApplicationCookie=", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(_authenticationCookie))
                        {
                            _authenticationCookie = _authenticationCookie.Split(';')[0];
                            _httpClient.DefaultRequestHeaders.Add("Cookie", _authenticationCookie);
                            Console.WriteLine($"[DaminionApiClient] Authentication cookie found and applied: {_authenticationCookie.Substring(0, Math.Min(_authenticationCookie.Length, 50))}...");
                            return true;
                        }
                        else
                        {
                            Console.Error.WriteLine("[DaminionApiClient] Login HTTP call successful, but 'AspNet.ApplicationCookie' not found in 'Set-Cookie' header.");
                            _authenticationCookie = null;
                            return false;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("[DaminionApiClient] Login HTTP call successful, but 'Set-Cookie' header was not found in the response.");
                        _authenticationCookie = null;
                        return false;
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[DaminionApiClient] Login HTTP call failed. Full Response Body: {responseBody}");
                    _authenticationCookie = null;
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error during login to {loginUrl}: {ex.Message}");
                if (ex.InnerException != null) { Console.Error.WriteLine($"[DaminionApiClient] Inner Exception: {ex.InnerException.Message}"); }
                Console.Error.WriteLine($"[DaminionApiClient] HttpRequestException StackTrace: {ex.StackTrace}");
                _authenticationCookie = null;
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] Login request to {loginUrl} timed out after {_httpClient.Timeout.TotalSeconds} seconds: {ex.Message}");
                if (ex.InnerException != null) { Console.Error.WriteLine($"[DaminionApiClient] Inner Exception (Timeout): {ex.InnerException.Message}"); }
                _authenticationCookie = null;
                throw new HttpRequestException($"Login request timed out. Timeout: {_httpClient.Timeout.TotalSeconds}s.", ex);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred during login: {ex.Message}");
                if (ex.InnerException != null) { Console.Error.WriteLine($"[DaminionApiClient] Inner Exception: {ex.InnerException.Message}"); }
                Console.Error.WriteLine($"[DaminionApiClient] Exception StackTrace: {ex.StackTrace}");
                _authenticationCookie = null;
                throw;
            }
        }

        public async Task<List<DaminionTag>?> GetTagsAsync()
        {
            Console.WriteLine("[DaminionApiClient] Attempting GetTagsAsync...");
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] GetTags Error: Client is not authenticated or API base URL is not set.");
                return null;
            }

            string tagsUrl = $"{_apiBaseUrl}/api/settings/getTags";
            Console.WriteLine($"[DaminionApiClient] GetTags URL: {tagsUrl}");
            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(tagsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DaminionApiClient] GetTags Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    DaminionGetTagsResponse? getTagsResponse = null;
                    try
                    {
                        getTagsResponse = JsonSerializer.Deserialize<DaminionGetTagsResponse>(responseBody);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] Error deserializing GetTags response: {jsonEx.Message}. Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return null;
                    }

                    if (getTagsResponse != null && getTagsResponse.Success)
                    {
                        Console.WriteLine($"[DaminionApiClient] Successfully fetched {getTagsResponse.Data?.Count ?? 0} tags.");
                        return getTagsResponse.Data;
                    }
                    else
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] GetTags API call reported failure or bad data. Success: {getTagsResponse?.Success}, Error: {getTagsResponse?.Error}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return null;
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[DaminionApiClient] GetTags HTTP call failed. Status: {response.StatusCode}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error during GetTags: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred during GetTags: {ex.Message}");
                return null;
            }
        }

        public async Task<DaminionPathResult> GetAbsolutePathsAsync(List<long> itemIds)
        {
            Console.WriteLine($"[DaminionApiClient] Attempting GetAbsolutePathsAsync for {itemIds?.Count} items...");
            var result = new DaminionPathResult { Success = false, Paths = new Dictionary<string, string>() };
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                result.ErrorMessage = "Client is not authenticated or API base URL is not set.";
                Console.Error.WriteLine($"[DaminionApiClient] GetAbsolutePaths Error: {result.ErrorMessage}");
                return result;
            }
            if (itemIds == null || !itemIds.Any())
            {
                result.ErrorMessage = "Item IDs list cannot be null or empty.";
                Console.Error.WriteLine($"[DaminionApiClient] GetAbsolutePaths Error: {result.ErrorMessage}");
                return result;
            }

            string idsQueryParam = string.Join(",", itemIds);
            string pathsUrl = $"{_apiBaseUrl}/api/mediaItems/getAbsolutePaths?ids={idsQueryParam}";
            Console.WriteLine($"[DaminionApiClient] GetAbsolutePaths URL: {pathsUrl}");

            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(pathsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DaminionApiClient] GetAbsolutePaths Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    Dictionary<string, string>? paths = null;
                    try
                    {
                        paths = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] Failed to deserialize paths directly: {ex.Message}. Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        result.ErrorMessage = $"Failed to deserialize paths response: {ex.Message}";
                    }

                    if (paths != null && paths.Any())
                    {
                        result.Paths = paths;
                        result.Success = true;
                        Console.WriteLine($"[DaminionApiClient] Successfully fetched {paths.Count} absolute paths.");
                    }
                    else if (string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        result.ErrorMessage = "Successfully called API for paths, but no paths data was returned or parsed correctly.";
                        Console.Error.WriteLine($"[DaminionApiClient] {result.ErrorMessage} Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    }
                    return result;
                }
                else
                {
                    result.ErrorMessage = $"API call for paths failed. Status: {response.StatusCode}";
                    Console.Error.WriteLine($"[DaminionApiClient] {result.ErrorMessage} Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error getting absolute paths: {ex.Message}";
                Console.Error.WriteLine($"[DaminionApiClient] GetAbsolutePaths Exception: {ex.Message} StackTrace: {ex.StackTrace}");
                return result;
            }
        }

        public async Task<DaminionBatchChangeResponse?> UpdateItemMetadataAsync(List<long> itemIds, List<DaminionUpdateOperation> operations)
        {
            Console.WriteLine($"[DaminionApiClient] Attempting UpdateItemMetadataAsync for {itemIds?.Count} items with {operations?.Count} operations...");
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] UpdateItemMetadata Error: Client is not authenticated or API base URL is not set.");
                return new DaminionBatchChangeResponse { Success = false, Error = "Not authenticated." };
            }
            if (itemIds == null || !itemIds.Any() || operations == null || !operations.Any())
            {
                Console.Error.WriteLine("[DaminionApiClient] UpdateItemMetadata Error: Item IDs and operations list cannot be null or empty.");
                return new DaminionBatchChangeResponse { Success = false, Error = "Item IDs or operations missing." };
            }

            string updateUrl = $"{_apiBaseUrl}/api/itemData/batchChange";
            Console.WriteLine($"[DaminionApiClient] UpdateItemMetadata URL: {updateUrl}");
            var requestPayload = new DaminionBatchChangeRequest
            {
                Ids = itemIds,
                Data = operations
            };

            string jsonRequest = "";
            try
            {
                jsonRequest = JsonSerializer.Serialize(requestPayload);
                Console.WriteLine($"[DaminionApiClient] UpdateItemMetadata Request Payload (snippet): {jsonRequest.Substring(0, Math.Min(jsonRequest.Length, 500))}...");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] Error serializing update metadata request: {ex.Message}");
                return new DaminionBatchChangeResponse { Success = false, Error = $"Serialization error: {ex.Message}" };
            }
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(updateUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DaminionApiClient] UpdateItemMetadata Response Status Code: {response.StatusCode}");

                DaminionBatchChangeResponse? batchChangeResponse = null;
                try
                {
                    batchChangeResponse = JsonSerializer.Deserialize<DaminionBatchChangeResponse>(responseBody);
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    Console.Error.WriteLine($"[DaminionApiClient] Error deserializing batchChange response: {jsonEx.Message}. Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    if (response.IsSuccessStatusCode) return new DaminionBatchChangeResponse { Success = true, Error = "Response was not valid JSON, but HTTP status was success." };
                    return new DaminionBatchChangeResponse { Success = false, Error = $"JSON Deserialization error: {jsonEx.Message}" };
                }

                if (batchChangeResponse == null)
                {
                    if (response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(responseBody))
                    {
                        Console.WriteLine("[DaminionApiClient] UpdateItemMetadata returned success status with empty body. Assuming success.");
                        return new DaminionBatchChangeResponse { Success = true };
                    }
                    Console.Error.WriteLine($"[DaminionApiClient] Failed to deserialize batchChange response or response object was null. Status: {response.StatusCode}, Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return new DaminionBatchChangeResponse { Success = false, Error = $"Failed to deserialize response or null object. Status: {response.StatusCode}" };
                }

                if (!batchChangeResponse.Success)
                {
                    Console.Error.WriteLine($"[DaminionApiClient] UpdateItemMetadata operation reported failure. Error: {batchChangeResponse.Error}. Body(snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                }
                else
                {
                    Console.WriteLine("[DaminionApiClient] UpdateItemMetadata reported success.");
                }
                return batchChangeResponse;
            }
            catch (HttpRequestException httpEx)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error updating item metadata: {httpEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred updating item metadata: {ex.Message}");
                return new DaminionBatchChangeResponse { Success = false, Error = $"Exception: {ex.Message}" };
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal class LoginRequest
    {
        [JsonPropertyName("usernameOrEmailAddress")]
        public string UsernameOrEmailAddress { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}