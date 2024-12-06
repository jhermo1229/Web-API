using DatabaseQueryAPI.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class DatabaseService
    {
        // Your connection string
        private readonly string _connectionString = "Server=144.217.253.105;Database=sanigear_db;User ID=sanigear_office;Password=3]E-pMwvwQ}C;";

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery, Dictionary<string, object> parameters)
        {
            var results = new List<Dictionary<string, object>>();
            Debug.WriteLine($"Database Service: {sqlQuery}");

            try
            {
                // Establish the connection to the database
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sqlQuery, connection))
                    {
                        // Add parameters if they exist, ensuring correct types
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                // Ensure that parameter is converted to an appropriate type if needed
                                var paramValue = param.Value;

                                // Check if the parameter is a JsonElement
                                if (paramValue is JsonElement jsonElement)
                                {
                                    // If the JsonElement is an object or array, you need to handle it
                                    // For example, convert it to a string (you can modify this based on your needs)
                                    paramValue = jsonElement.ToString(); // Convert JSON to string
                                    command.Parameters.AddWithValue(param.Key, paramValue);
                                }
                                // Convert DateTime to MySQL-friendly string format
                                else if (paramValue is DateTime dateTimeValue)
                                {
                                    command.Parameters.AddWithValue(param.Key, dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss"));
                                }
                                // Convert boolean to 0/1 for MySQL compatibility
                                else if (paramValue is bool boolValue)
                                {
                                    command.Parameters.AddWithValue(param.Key, boolValue ? 1 : 0);
                                }
                                // Convert null to DBNull for database compatibility
                                else if (paramValue == null)
                                {
                                    command.Parameters.AddWithValue(param.Key, DBNull.Value);
                                }
                                else
                                {
                                    // Add the parameter normally if it's already in a supported type
                                    command.Parameters.AddWithValue(param.Key, paramValue);
                                }
                            }
                        }

                        // Execute the query and read the results
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }

                                Debug.WriteLine($"Received row: {string.Join(", ", row)}");  // Log the row contents for debugging
                                results.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error and throw it again to ensure proper error reporting
                Debug.WriteLine($"Error executing query: {ex.Message}");
                throw new InvalidOperationException("An error occurred while executing the query.", ex);
            }

            return results;
        }
    }
}
