using DatabaseQueryAPI.Models;
using MySqlConnector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;            // <-- add
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseService> _logger;
        private readonly IReadOnlyDictionary<string, string> _ipMap;

        // Inject IConfiguration so we can read ConnectionStrings + IpMappings
        public DatabaseService(IConfiguration config, ILogger<DatabaseService> logger)
        {
            _logger = logger;

            // Prefer config; if missing, fall back to your existing literal
            _connectionString = config.GetConnectionString("MainDb");

            _ipMap = config.GetSection("IpMappings").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string sqlQuery,
            Dictionary<string, object> parameters,
            string userIdentifier = "Unknown",
            string clientIp = "Unknown")
        {
            var results = new List<Dictionary<string, object>>();

            // Log the user, labeled IP, and the query being executed
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
                                    // Keep your existing formatting
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
                _logger.LogError(ex, "Error executing query: {Message}", ex.Message);
                throw new InvalidOperationException("An error occurred while executing the query.", ex);
            }

            return results;
        }

        // Method to log query execution details
        private void LogQueryExecution(
            string sqlQuery,
            Dictionary<string, object> parameters,
            string userIdentifier,
            string clientIpRaw)
        {
            // Enrich IP with friendly name if present
            string clientIpLabeled = LabelIp(clientIpRaw);

            // Timestamps: UTC and Ontario local
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
            DateTimeOffset localNow = TimeZoneInfo.ConvertTime(utcNow, tz);

            var logMessage =
                $"User '{userIdentifier}' from IP '{clientIpLabeled}' " +
                $"at UTC {utcNow:O} (Ontario {localNow:yyyy-MM-dd HH:mm:ss zzz}) executed query: {sqlQuery}";

            if (parameters != null && parameters.Count > 0)
            {
                logMessage += " | Params: " + string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
            }

            _logger.LogInformation(logMessage);
        }

        private string LabelIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return "Unknown";
            return _ipMap.TryGetValue(ip, out var name) ? $"{ip} ({name})" : ip;
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
