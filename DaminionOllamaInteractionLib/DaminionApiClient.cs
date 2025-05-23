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
using DaminionOllamaInteractionLib.Daminion;
using DaminionOllamaInteractionLib.Ollama; // For Daminion DTOs

namespace DaminionOllamaInteractionLib
{
    /// <summary>
    /// Represents a client for interacting with the Daminion API.
    /// </summary>
    public class DaminionApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string? _apiBaseUrl;
        private string? _authenticationCookie;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaminionApiClient"/> class.
        /// </summary>
        public DaminionApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            Console.WriteLine("[DaminionApiClient] Initialized.");
        }

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

            // Ollama's root endpoint typically returns "Ollama is running" with a 200 OK.
            string healthCheckUrl = _apiBaseUrl; // Or a specific health check endpoint like /api/ps or similar if available
            Console.WriteLine($"[OllamaApiClient] Checking Ollama connection at: {healthCheckUrl}");

            try
            {
                // Use a shorter timeout for a simple connection check
                using (var tempHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    HttpResponseMessage response = await tempHttpClient.GetAsync(healthCheckUrl);
                    Console.WriteLine($"[OllamaApiClient] Connection check response status: {response.StatusCode}");
                    //string responseBody = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"[OllamaApiClient] Connection check response body: {responseBody}");
                    return response.IsSuccessStatusCode; // Or check for specific content if needed
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OllamaApiClient] Error checking Ollama connection: {ex.Message}");
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
                // _httpClient is the class member HttpClient, already configured
                HttpResponseMessage response = await _httpClient.GetAsync(listModelsUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OllamaApiClient] ListLocalModels Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    OllamaListTagsResponse? listResponse = null;
                    try
                    {
                        listResponse = JsonSerializer.Deserialize<OllamaListTagsResponse>(responseBody,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // Ollama sometimes uses snake_case
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[OllamaApiClient] Error deserializing ListLocalModels response: {jsonEx.Message}. Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return null; // Or a response object indicating failure
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
                    return null; // Or a response object indicating failure
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OllamaApiClient] An unexpected error occurred during ListLocalModels: {ex.Message}");
                return null; // Or a response object indicating failure
            }
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

                        _authenticationCookie = cookieValues.FirstOrDefault(c => c.StartsWith(".AspNet.ApplicationCookie=", StringComparison.OrdinalIgnoreCase));
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

        /// <summary>
        /// Asynchronously retrieves the list of tags from the Daminion server.
        /// </summary>
        /// <returns></returns>
        // Inside DaminionApiClient class in DaminionOllamaInteractionLib project

        // Ensure the return type is Task<DaminionGetTagsResponse?>
        public async Task<DaminionGetTagsResponse?> GetTagsAsync()
        {
            Console.WriteLine("[DaminionApiClient] Attempting GetTagsAsync...");
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] GetTags Error: Client is not authenticated or API base URL is not set.");
                // Return the response object with error details
                return new DaminionGetTagsResponse { Success = false, Error = "Not authenticated or API base URL not set." };
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
                    catch (JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] Error deserializing GetTags response: {jsonEx.Message}. Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        // Return the response object with error details
                        return new DaminionGetTagsResponse { Success = false, Error = $"JSON Deserialization error: {jsonEx.Message}" };
                    }

                    // Return the entire DaminionGetTagsResponse object
                    if (getTagsResponse != null) // Check if deserialization was successful
                    {
                        if (getTagsResponse.Success)
                        {
                            Console.WriteLine($"[DaminionApiClient] Successfully fetched {getTagsResponse.Data?.Count ?? 0} tags.");
                        }
                        else
                        {
                            Console.Error.WriteLine($"[DaminionApiClient] GetTags API call reported failure or bad data. Success: {getTagsResponse.Success}, Error: {getTagsResponse.Error}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        }
                        return getTagsResponse;
                    }
                    else
                    {
                        // Should not happen if deserialization didn't throw, but as a fallback
                        Console.Error.WriteLine($"[DaminionApiClient] GetTags deserialization resulted in null object. Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return new DaminionGetTagsResponse { Success = false, Error = "Deserialization resulted in null object." };
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[DaminionApiClient] GetTags HTTP call failed. Status: {response.StatusCode}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    // Return the response object with error details
                    return new DaminionGetTagsResponse { Success = false, Error = $"HTTP Error: {response.StatusCode}" };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error during GetTags: {ex.Message}");
                return new DaminionGetTagsResponse { Success = false, Error = $"HTTP Request Exception: {ex.Message}" };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred during GetTags: {ex.Message}");
                return new DaminionGetTagsResponse { Success = false, Error = $"Unexpected Exception: {ex.Message}" };
            }
        }
        /// <summary>
        /// Asynchronously retrieves the absolute paths of media items from the Daminion server.
        /// </summary>
        /// <param name="itemIds"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Searches for media items in Daminion based on a queryLine and operators (f parameter).
        /// Corresponds to Daminion API endpoint GET /api/mediaItems/get.
        /// </summary>
        /// <param name="queryLine">Search conditions separated by a semicolon[cite: 69]. Optional[cite: 70].</param>
        /// <param name="f_operators">Logical operators separated by a semicolon (the 'f' parameter)[cite: 71]. Optional[cite: 72].</param>
        /// <param name="pageSize">Page size. Can take positive integer values from 0 to 1000[cite: 73]. Optional, defaults to 0 if not specified[cite: 74].</param>
        /// <param name="pageIndex">The serial number of the requested page[cite: 75]. Optional, defaults to 0 if not specified[cite: 76].</param>
        /// <returns>A DaminionSearchMediaItemsResponse containing the media items or an error.</returns>
        public async Task<DaminionSearchMediaItemsResponse?> SearchMediaItemsAsync(
            string? queryLine = null, // Made queryLine nullable as it's optional
            string? f_operators = null,
            int pageSize = 100, // Defaulting to a reasonable page size
            int pageIndex = 0)
        {
            Console.WriteLine($"[DaminionApiClient] Attempting SearchMediaItemsAsync with queryLine: '{queryLine}' and f_operators: '{f_operators}'");
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] SearchMediaItems Error: Client is not authenticated or API base URL is not set.");
                return new DaminionSearchMediaItemsResponse { Success = false, Error = "Client not authenticated." };
            }

            var queryParams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(queryLine))
            {
                queryParams.Add("queryLine", queryLine); // [cite: 69]
            }
            if (!string.IsNullOrEmpty(f_operators))
            {
                queryParams.Add("f", f_operators); // [cite: 71]
            }
            // API doc says page size 0-1000[cite: 73].
            // If not specified, page size is 0[cite: 74], which might mean "all" or "default", be cautious.
            // For robust pagination, always specify a positive page size if you expect many results.
            queryParams.Add("size", pageSize.ToString()); // [cite: 73]
            queryParams.Add("index", pageIndex.ToString()); // [cite: 75]
                                                            // sortTag and sort parameters are also available if needed [cite: 77, 79]

            string queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            string searchUrl = $"{_apiBaseUrl}/api/mediaItems/get?{queryString}"; // [cite: 69]
            Console.WriteLine($"[DaminionApiClient] SearchMediaItems URL: {searchUrl}");

            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(searchUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DaminionApiClient] SearchMediaItems Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    DaminionSearchMediaItemsResponse? searchResponse = null;
                    try
                    {
                        searchResponse = System.Text.Json.JsonSerializer.Deserialize<DaminionSearchMediaItemsResponse>(responseBody);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] Error deserializing SearchMediaItems response: {jsonEx.Message}. Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return new DaminionSearchMediaItemsResponse { Success = false, Error = $"JSON Deserialization error: {jsonEx.Message}" };
                    }

                    if (searchResponse != null && searchResponse.Success) // [cite: 83]
                    {
                        Console.WriteLine($"[DaminionApiClient] Successfully fetched {searchResponse.MediaItems?.Count ?? 0} media items.");
                        return searchResponse; // [cite: 83]
                    }
                    else
                    {
                        string errorMsg = searchResponse?.Error ?? "API call reported failure or bad data."; // [cite: 82]
                        Console.Error.WriteLine($"[DaminionApiClient] SearchMediaItems API call failed or returned unsuccessful. Success: {searchResponse?.Success}, Error: {errorMsg}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return new DaminionSearchMediaItemsResponse { Success = false, Error = errorMsg, MediaItems = searchResponse?.MediaItems };
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[DaminionApiClient] SearchMediaItems HTTP call failed. Status: {response.StatusCode}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return new DaminionSearchMediaItemsResponse { Success = false, Error = $"HTTP Error: {response.StatusCode}" };
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error during SearchMediaItems: {ex.Message}");
                return new DaminionSearchMediaItemsResponse { Success = false, Error = $"HTTP Request Exception: {ex.Message}" };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred during SearchMediaItems: {ex.Message}");
                return new DaminionSearchMediaItemsResponse { Success = false, Error = $"Unexpected Exception: {ex.Message}" };
            }
        }


        /// <summary>
        /// Asynchronously updates the metadata of items in Daminion.
        /// </summary>
        /// <param name="itemIds"></param>
        /// <param name="operations"></param>
        /// <returns></returns>
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

        // Inside DaminionApiClient class

        /// <summary>
        /// Asynchronously retrieves the values for a specified indexed tag from the Daminion server.
        /// </summary>
        /// <param name="indexedTagId">ID of the tag whose values should be found. [cite: 23]</param>
        /// <param name="pageSize">Page size (positive integer, max 2,147,483,647). [cite: 24]</param>
        /// <param name="pageIndex">Page serial number (0 to 2,147,483,647). [cite: 25]</param>
        /// <param name="parentValueId">Limits search level. 0 for root, -2 for thorough search, or specific parent tag value ID. [cite: 26]</param>
        /// <param name="filter">Case-insensitive search string to filter values. [cite: 27]</param>
        /// <returns>A DaminionGetTagValuesResponse containing the tag values or an error.</returns>
        public async Task<DaminionGetTagValuesResponse?> GetTagValuesAsync(
            long indexedTagId,
            int pageSize = 100, // Default page size
            int pageIndex = 0,
            long parentValueId = 0, // Default to root level [cite: 26]
            string filter = "")
        {
            Console.WriteLine($"[DaminionApiClient] Attempting GetTagValuesAsync for tag ID: {indexedTagId}...");
            if (!IsAuthenticated || string.IsNullOrEmpty(_apiBaseUrl))
            {
                Console.Error.WriteLine("[DaminionApiClient] GetTagValues Error: Client is not authenticated or API base URL is not set.");
                return new DaminionGetTagValuesResponse { Success = false, Error = "Client not authenticated." };
            }

            // Construct the query parameters
            var queryParams = new Dictionary<string, string>
    {
        { "indexedTagId", indexedTagId.ToString() },
        { "pageSize", pageSize.ToString() },
        { "pageIndex", pageIndex.ToString() },
        { "parentValueId", parentValueId.ToString() }
    };
            if (!string.IsNullOrEmpty(filter))
            {
                queryParams.Add("filter", filter);
            }

            string queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
            string getTagValuesUrl = $"{_apiBaseUrl}/api/indexedTagValues/getIndexedTagValues?{queryString}";
    Console.WriteLine($"[DaminionApiClient] GetTagValues URL: {getTagValuesUrl}");

            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.GetAsync(getTagValuesUrl);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DaminionApiClient] GetTagValues Response Status Code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    DaminionGetTagValuesResponse? getValuesResponse = null;
                    try
                    {
                        getValuesResponse = JsonSerializer.Deserialize<DaminionGetTagValuesResponse>(responseBody);
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.Error.WriteLine($"[DaminionApiClient] Error deserializing GetTagValues response: {jsonEx.Message}. Body: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return new DaminionGetTagValuesResponse { Success = false, Error = $"JSON Deserialization error: {jsonEx.Message}" };
                    }

                    if (getValuesResponse != null && getValuesResponse.Success)
                    {
                        Console.WriteLine($"[DaminionApiClient] Successfully fetched {getValuesResponse.Values?.Count ?? 0} tag values for tag ID {indexedTagId}.");
                        return getValuesResponse;
                    }
                    else
                    {
                        string errorMsg = getValuesResponse?.Error ?? "API call reported failure or bad data.";
                        Console.Error.WriteLine($"[DaminionApiClient] GetTagValues API call failed or returned unsuccessful. Success: {getValuesResponse?.Success}, Error: {errorMsg}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                        return new DaminionGetTagValuesResponse { Success = false, Error = errorMsg, Values = getValuesResponse?.Values /* preserve values if any */ };
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[DaminionApiClient] GetTagValues HTTP call failed. Status: {response.StatusCode}, Body (snippet): {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                    return new DaminionGetTagValuesResponse { Success = false, Error = $"HTTP Error: {response.StatusCode}" };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] HTTP request error during GetTagValues: {ex.Message}");
                return new DaminionGetTagValuesResponse { Success = false, Error = $"HTTP Request Exception: {ex.Message}" };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DaminionApiClient] An unexpected error occurred during GetTagValues: {ex.Message}");
                return new DaminionGetTagValuesResponse { Success = false, Error = $"Unexpected Exception: {ex.Message}" };
            }
        }

    }

    /// <summary>
    /// Represents the request payload for logging in to Daminion.
    /// </summary>
    internal class LoginRequest
    {
        [JsonPropertyName("usernameOrEmailAddress")]
        public string UsernameOrEmailAddress { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}