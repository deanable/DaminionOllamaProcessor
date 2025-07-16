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
    public static class GoogleCloudResourceHelper
    {
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