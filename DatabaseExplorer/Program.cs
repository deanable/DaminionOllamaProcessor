using System;
using System.Data;
using System.Threading.Tasks;

namespace DatabaseExplorer
{
    /// <summary>
    /// The main entry point for the Database Explorer application.
    /// </summary>
    class Program
    {
        /// <summary>
        /// The main entry point for the Database Explorer application.
        /// This application connects to a Daminion database and performs several exploration tasks:
        /// 1. Retrieves and lists all table names.
        /// 2. Finds and lists tables that may contain tag-related data.
        /// 3. Attempts to retrieve and display sample data from common tag-related tables.
        /// 4. Explores the structure and sample data of other potentially interesting tables.
        /// </summary>
        /// <param name="args">Command-line arguments (not used).</param>
        static async Task Main(string[] args)
        {
            Console.WriteLine("Daminion Database Explorer");
            Console.WriteLine("=========================");
            
            try
            {
                // NOTE: Connection details are hardcoded for simplicity.
                // In a real application, these should be moved to a configuration file.
                var explorer = new DatabaseExplorer(
                    host: "192.168.60.7",
                    database: "NetCatalog",
                    username: "postgres",
                    password: "postgres"
                );

                Console.WriteLine("Connecting to database...");
                
                // 1. Get all table names
                Console.WriteLine("\n1. Getting all tables...");
                var tables = await explorer.GetTableNamesAsync();
                Console.WriteLine($"Found {tables.Count} tables:");
                foreach (var table in tables)
                {
                    Console.WriteLine($"  - {table}");
                }

                // 2. Find tag-related tables
                Console.WriteLine("\n2. Finding tag-related tables...");
                var tagTables = await explorer.FindTagsAsync();
                Console.WriteLine("Tag-related tables:");
                foreach (DataRow row in tagTables.Rows)
                {
                    Console.WriteLine($"  - {row["table_name"]}");
                }

                // 3. Try to get tag data
                Console.WriteLine("\n3. Exploring tag data...");
                var tagData = await explorer.GetTagDataAsync();
                if (tagData.Rows.Count > 0)
                {
                    Console.WriteLine("Tag data found:");
                    foreach (DataColumn col in tagData.Columns)
                    {
                        Console.Write($"{col.ColumnName}\t");
                    }
                    Console.WriteLine();
                    
                    foreach (DataRow row in tagData.Rows)
                    {
                        foreach (DataColumn col in tagData.Columns)
                        {
                            Console.Write($"{row[col]}\t");
                        }
                        Console.WriteLine();
                    }
                }

                // 4. Look for specific tables that might contain tag information
                var interestingTables = new[] { "tags", "tag", "keywords", "keyword", "categories", "category", "items", "media", "files" };
                
                Console.WriteLine("\n4. Exploring interesting tables...");
                foreach (var tableName in interestingTables)
                {
                    if (tables.Contains(tableName))
                    {
                        Console.WriteLine($"\nTable: {tableName}");
                        Console.WriteLine("Structure:");
                        var structure = await explorer.GetTableStructureAsync(tableName);
                        foreach (DataRow row in structure.Rows)
                        {
                            Console.WriteLine($"  {row["column_name"]} ({row["data_type"]}) - Nullable: {row["is_nullable"]}");
                        }
                        
                        Console.WriteLine("Sample data:");
                        var sampleData = await explorer.GetSampleDataAsync(tableName, 3);
                        if (sampleData.Rows.Count > 0)
                        {
                            foreach (DataColumn col in sampleData.Columns)
                            {
                                Console.Write($"{col.ColumnName}\t");
                            }
                            Console.WriteLine();
                            
                            foreach (DataRow row in sampleData.Rows)
                            {
                                foreach (DataColumn col in sampleData.Columns)
                                {
                                    Console.Write($"{row[col]}\t");
                                }
                                Console.WriteLine();
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
