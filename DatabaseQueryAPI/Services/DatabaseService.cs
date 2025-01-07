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
        private readonly string _connectionString = "Server=144.217.253.105;Database=sanigear_db;User ID=sanigear_office;Password=3]E-pMwvwQ}C;";

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery, Dictionary<string, object> parameters, string userIdentifier = "Unknown")
        {
            var results = new List<Dictionary<string, object>>();

            // Log the user who is making the request and the query being executed
            LogQueryExecution(sqlQuery, parameters, userIdentifier);

            try
            {
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sqlQuery, connection))
                    {
                        // Add parameters to the command if they exist
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                var paramValue = param.Value;

                                if (paramValue is JsonElement jsonElement)
                                {
                                    paramValue = jsonElement.ToString();
                                }
                                else if (paramValue is DateTime dateTimeValue)
                                {
                                    paramValue = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else if (paramValue is bool boolValue)
                                {
                                    paramValue = boolValue ? 1 : 0;
                                }
                                else if (paramValue == null)
                                {
                                    paramValue = DBNull.Value;
                                }

                                command.Parameters.AddWithValue(param.Key, paramValue);
                            }
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }

                                results.Add(row);
                            }
                        }

                        // Log the results after the query is executed
                        LogQueryResults(results);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error and throw it again
                Debug.WriteLine($"Error executing query: {ex.Message}");
                throw new InvalidOperationException("An error occurred while executing the query.", ex);
            }

            return results;
        }

        // Method to log query execution details
        private void LogQueryExecution(string sqlQuery, Dictionary<string, object> parameters, string userIdentifier)
        {
            var logMessage = $"User '{userIdentifier}' executed the following query: {sqlQuery}";
            if (parameters != null && parameters.Count > 0)
            {
                logMessage += " With parameters: " + string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
            }
            Debug.WriteLine(logMessage);
        }

        // Method to log the query result details
        private void LogQueryResults(List<Dictionary<string, object>> results)
        {
            if (results != null && results.Count > 0)
            {
                Debug.WriteLine($"Query returned {results.Count} rows.");
                foreach (var row in results)
                {
                    var rowData = string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    Debug.WriteLine($"Result Row: {rowData}");
                }
            }
            else
            {
                Debug.WriteLine("Query returned no results.");
            }
        }
    }
}
