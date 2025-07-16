using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using Google.Apis.Auth.OAuth2;
using System.Net.Http.Headers;

namespace DaminionOllamaApp.Services
{
    public class GoogleBillingApiClient
    {
        private readonly string _serviceAccountJsonPath;
        private string? _accessToken;
        private DateTime _accessTokenExpiry;
        private static readonly string[] Scopes = new[] { "https://www.googleapis.com/auth/cloud-billing.readonly" };

        public GoogleBillingApiClient(string serviceAccountJsonPath)
        {
            _serviceAccountJsonPath = serviceAccountJsonPath;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiry)
            {
                return _accessToken;
            }
            GoogleCredential credential;
            using (var stream = new FileStream(_serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }
            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            _accessToken = token;
            _accessTokenExpiry = DateTime.UtcNow.AddMinutes(50);
            return _accessToken;
        }

        /// <summary>
        /// Fetches the current month's spend for the given project using the Cloud Billing API.
        /// </summary>
        /// <param name="projectId">The GCP project ID (e.g., "my-gemini-project")</param>
        /// <returns>The spend in USD, or -1 if unavailable.</returns>
        public async Task<double> GetCurrentMonthSpendAsync(string projectId)
        {
            // NOTE: The Cloud Billing API does not provide real-time spend per project directly.
            // For a production implementation, use BigQuery billing export or Budgets API for more detail.
            // Here, we simulate a call and return a stub value.

            // Example endpoint: GET https://cloudbilling.googleapis.com/v1/projects/{projectId}/billingInfo
            // This only returns billing account info, not spend.

            // For demonstration, return a stub value:
            await Task.Delay(100); // Simulate network
            return -1; // -1 means unavailable
        }
    }
} 