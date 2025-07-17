using System;
using System.Threading.Tasks;
using Google.Cloud.BigQuery.V2;
using Google.Apis.Auth.OAuth2;
using System.IO;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Provides methods to query Google BigQuery billing export tables for current month spend.
    /// </summary>
    public class BigQueryBillingClient
    {
        /// <summary>
        /// The Google Cloud project ID.
        /// </summary>
        private readonly string _projectId;
        /// <summary>
        /// The BigQuery dataset name.
        /// </summary>
        private readonly string _dataset;
        /// <summary>
        /// The BigQuery table name.
        /// </summary>
        private readonly string _table;
        /// <summary>
        /// Path to the service account JSON file for authentication.
        /// </summary>
        private readonly string _serviceAccountJsonPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigQueryBillingClient"/> class.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="dataset">The BigQuery dataset name.</param>
        /// <param name="table">The BigQuery table name.</param>
        /// <param name="serviceAccountJsonPath">Path to the service account JSON file.</param>
        public BigQueryBillingClient(string projectId, string dataset, string table, string serviceAccountJsonPath)
        {
            _projectId = projectId;
            _dataset = dataset;
            _table = table;
            _serviceAccountJsonPath = serviceAccountJsonPath;
        }

        /// <summary>
        /// Gets the total spend in USD for the current month from the billing export table.
        /// </summary>
        /// <returns>The total spend in USD for the current month.</returns>
        public async Task<double> GetCurrentMonthSpendUSDAsync()
        {
            // Log: Starting billing fetch
            System.Diagnostics.Debug.WriteLine("[BigQueryBillingClient] Starting GetCurrentMonthSpendUSDAsync");
            // Authenticate using the service account JSON
            GoogleCredential credential;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Opening service account JSON: {_serviceAccountJsonPath}");
                using (var stream = new FileStream(_serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Failed to load service account JSON: {ex.Message}");
                throw;
            }
            BigQueryClient client;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Creating BigQueryClient for project: {_projectId}");
                client = await BigQueryClient.CreateAsync(_projectId, credential);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Failed to create BigQueryClient: {ex.Message}");
                throw;
            }

            // Get the first and last day of the current month
            var now = DateTime.UtcNow;
            var firstDay = new DateTime(now.Year, now.Month, 1);
            var nextMonth = firstDay.AddMonths(1);

            // Standard GCP billing export schema: cost is in the 'cost' field, usage_start_time is the timestamp
            string query = $@"
                SELECT SUM(cost) as total_cost
                FROM `{_projectId}.{_dataset}.{_table}`
                WHERE usage_start_time >= TIMESTAMP('{firstDay:yyyy-MM-dd}')
                  AND usage_start_time < TIMESTAMP('{nextMonth:yyyy-MM-dd}')
                  AND cost_type = 'regular'
            ";
            System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Executing query: {query.Replace("\n", " ").Replace("  ", " ")}");
            try
            {
                var result = await client.ExecuteQueryAsync(query, parameters: null);
                foreach (var row in result)
                {
                    System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Query result row: total_cost={row["total_cost"]}");
                    if (row["total_cost"] != null && double.TryParse(row["total_cost"].ToString(), out double cost))
                    {
                        System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Parsed total_cost: {cost}");
                        return cost;
                    }
                }
                System.Diagnostics.Debug.WriteLine("[BigQueryBillingClient] No rows returned or total_cost is null.");
                return 0.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BigQueryBillingClient] Query execution failed: {ex.Message}");
                throw;
            }
        }
    }
} 