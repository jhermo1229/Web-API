using DatabaseQueryAPI.Models;
using MySqlConnector;
using Microsoft.Extensions.Logging;  // Add this namespace for ILogger
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "Server=144.217.253.105;Database=sanigear_db;User ID=sanigear_office;Password=3]E-pMwvwQ}C;";
        private readonly ILogger<DatabaseService> _logger;  // Declare logger

        // Constructor to inject the logger
        public DatabaseService(ILogger<DatabaseService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery, Dictionary<string, object> parameters, string userIdentifier = "Unknown", string clientIp = "Unknown")
        {
            var results = new List<Dictionary<string, object>>();

            // Log the user, IP address, and the query being executed
            LogQueryExecution(sqlQuery, parameters, userIdentifier, clientIp);

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
                _logger.LogError(ex, "Error executing query: {Message}", ex.Message);  // LogError for exceptions
                throw new InvalidOperationException("An error occurred while executing the query.", ex);
            }

            return results;
        }

        // Method to log query execution details
        private void LogQueryExecution(string sqlQuery, Dictionary<string, object> parameters, string userIdentifier, string clientIp)
        {
            var logMessage = $"User '{userIdentifier}' from IP '{clientIp}' executed the following query: {sqlQuery}";
            if (parameters != null && parameters.Count > 0)
            {
                logMessage += " With parameters: " + string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
            }

            // Log as Information
            _logger.LogInformation(logMessage);  // Use LogInformation for normal logs
        }

        // Method to log the query result details
        private void LogQueryResults(List<Dictionary<string, object>> results)
        {
            if (results != null && results.Count > 0)
            {
                _logger.LogInformation($"Query returned {results.Count} rows.");
                foreach (var row in results)
                {
                    var rowData = string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    _logger.LogInformation($"Result Row: {rowData}");
                }
            }
            else
            {
                _logger.LogInformation("Query returned no results.");
            }
        }
    }
}
