using System;
using System.Threading.Tasks;
using Google.Cloud.BigQuery.V2;
using Google.Apis.Auth.OAuth2;
using System.IO;

namespace DaminionOllamaApp.Services
{
    public class BigQueryBillingClient
    {
        private readonly string _projectId;
        private readonly string _dataset;
        private readonly string _table;
        private readonly string _serviceAccountJsonPath;

        public BigQueryBillingClient(string projectId, string dataset, string table, string serviceAccountJsonPath)
        {
            _projectId = projectId;
            _dataset = dataset;
            _table = table;
            _serviceAccountJsonPath = serviceAccountJsonPath;
        }

        public async Task<double> GetCurrentMonthSpendUSDAsync()
        {
            // Authenticate using the service account JSON
            GoogleCredential credential;
            using (var stream = new FileStream(_serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            var client = await BigQueryClient.CreateAsync(_projectId, credential);

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

            var result = await client.ExecuteQueryAsync(query, parameters: null);
            foreach (var row in result)
            {
                if (row["total_cost"] != null && double.TryParse(row["total_cost"].ToString(), out double cost))
                {
                    return cost;
                }
            }
            return 0.0;
        }
    }
} 