using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using System.Threading.Tasks;

namespace DatabaseExplorer
{
    /// <summary>
    /// Provides functionality to explore a PostgreSQL database, including retrieving table names,
    /// table structures, and sample data.
    /// </summary>
    public class DatabaseExplorer
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseExplorer"/> class.
        /// </summary>
        /// <param name="host">The database host.</param>
        /// <param name="database">The database name.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        public DatabaseExplorer(string host, string database, string username, string password)
        {
            _connectionString = $"Host={host};Database={database};Username={username};Password={password}";
        }

        /// <summary>
        /// Retrieves a list of table names from the public schema of the database.
        /// </summary>
        /// <returns>A list of table names.</returns>
        public async Task<List<string>> GetTableNamesAsync()
        {
            var tables = new List<string>();
            
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", 
                connection);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            
            return tables;
        }

        /// <summary>
        /// Retrieves the structure of a specified table.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>A <see cref="DataTable"/> containing the table structure.</returns>
        public async Task<DataTable> GetTableStructureAsync(string tableName)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(
                $"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = '{tableName}' ORDER BY ordinal_position", 
                connection);
            
            using var adapter = new NpgsqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        /// <summary>
        /// Retrieves sample data from a specified table.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="limit">The maximum number of rows to retrieve.</param>
        /// <returns>A <see cref="DataTable"/> containing the sample data.</returns>
        public async Task<DataTable> GetSampleDataAsync(string tableName, int limit = 10)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(
                $"SELECT * FROM \"{tableName}\" LIMIT {limit}", 
                connection);
            
            using var adapter = new NpgsqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        /// <summary>
        /// Finds tables that may contain tag-related data.
        /// </summary>
        /// <returns>A <see cref="DataTable"/> containing the names of potential tag tables.</returns>
        public async Task<DataTable> FindTagsAsync()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND (table_name ILIKE '%tag%' OR table_name ILIKE '%keyword%' OR table_name ILIKE '%category%')
                ORDER BY table_name";
            
            using var command = new NpgsqlCommand(query, connection);
            using var adapter = new NpgsqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        /// <summary>
        /// Retrieves sample data from common tag-related table names.
        /// </summary>
        /// <returns>A <see cref="DataTable"/> containing sample data from the first found tag table.</returns>
        public async Task<DataTable> GetTagDataAsync()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var possibleTables = new[] { "tags", "tag", "keywords", "keyword", "categories", "category" };
            
            foreach (var tableName in possibleTables)
            {
                try
                {
                    using var command = new NpgsqlCommand($"SELECT * FROM \"{tableName}\" LIMIT 5", connection);
                    using var adapter = new NpgsqlDataAdapter(command);
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    
                    if (dataTable.Rows.Count > 0)
                    {
                        Console.WriteLine($"Found data in table: {tableName}");
                        return dataTable;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Table {tableName} not found or error: {ex.Message}");
                }
            }
            
            return new DataTable();
        }
    }
}
