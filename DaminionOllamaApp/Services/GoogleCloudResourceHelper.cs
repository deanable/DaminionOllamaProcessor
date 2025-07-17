using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.CloudResourceManager.v1;
using Google.Cloud.BigQuery.V2;
using System.IO;
using System.Linq;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Provides helper methods for interacting with Google Cloud resources, such as listing projects, datasets, and tables.
    /// </summary>
    public static class GoogleCloudResourceHelper
    {
        /// <summary>
        /// Lists all active Google Cloud projects accessible with the given service account.
        /// </summary>
        /// <param name="serviceAccountJsonPath">Path to the service account JSON file.</param>
        /// <returns>A list of active project IDs.</returns>
        public static async Task<List<string>> ListProjectsAsync(string serviceAccountJsonPath)
        {
            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(CloudResourceManagerService.Scope.CloudPlatform);
            }
            var service = new CloudResourceManagerService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "DaminionOllamaApp"
            });
            var request = service.Projects.List();
            var result = await request.ExecuteAsync();
            var projects = new List<string>();
            if (result.Projects != null)
            {
                foreach (var project in result.Projects)
                {
                    if (project.LifecycleState == "ACTIVE")
                        projects.Add(project.ProjectId);
                }
            }
            return projects;
        }

        /// <summary>
        /// Lists all datasets in the specified project using the given service account.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="serviceAccountJsonPath">Path to the service account JSON file.</param>
        /// <returns>A list of dataset IDs.</returns>
        public static async Task<List<string>> ListDatasetsAsync(string projectId, string serviceAccountJsonPath)
        {
            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            var client = await BigQueryClient.CreateAsync(projectId, credential);
            var datasets = new List<string>();
            await foreach (var dataset in client.ListDatasetsAsync())
            {
                datasets.Add(dataset.Reference.DatasetId);
            }
            return datasets;
        }

        /// <summary>
        /// Lists all tables in the specified dataset using the given service account.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="datasetId">The dataset ID.</param>
        /// <param name="serviceAccountJsonPath">Path to the service account JSON file.</param>
        /// <returns>A list of table IDs.</returns>
        public static async Task<List<string>> ListTablesAsync(string projectId, string datasetId, string serviceAccountJsonPath)
        {
            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            var client = await BigQueryClient.CreateAsync(projectId, credential);
            var tables = new List<string>();
            await foreach (var table in client.ListTablesAsync(datasetId))
            {
                tables.Add(table.Reference.TableId);
            }
            return tables;
        }

        /// <summary>
        /// Determines if the specified table is a GCP billing export table by checking for required schema fields.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="datasetId">The dataset ID.</param>
        /// <param name="tableId">The table ID.</param>
        /// <param name="serviceAccountJsonPath">Path to the service account JSON file.</param>
        /// <returns>True if the table is a billing export table; otherwise, false.</returns>
        public static async Task<bool> IsBillingExportTableAsync(string projectId, string datasetId, string tableId, string serviceAccountJsonPath)
        {
            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            var client = await BigQueryClient.CreateAsync(projectId, credential);
            var table = await client.GetTableAsync(datasetId, tableId);
            // Check for required fields in the billing export schema
            return table.Schema.Fields.Any(f => f.Name == "cost") && table.Schema.Fields.Any(f => f.Name == "usage_start_time");
        }
    }
} 