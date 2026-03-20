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
            //  add connection string here _connectionString = config.GetConnectionString("MainDb") ?? "Server=144.217.253.105;Database=sanigear_db;User ID=;Password=;";
            _connectionString = config.GetConnectionString("MainDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:MainDb");

            _ipMap = config.GetSection("IpMappings").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string sqlQuery,
            Dictionary<string, object> parameters,
            string userIdentifier = "Unknown",
            string clientIp = "Unknown")
        {
            var results = new List<Dictionary<string, object>>();
            LogQueryExecution(sqlQuery, parameters, userIdentifier, clientIp);

            try
            {
                await using var connection = await OpenConnectionWithRetryAsync();

                await using var command = new MySqlCommand(sqlQuery, connection)
                {
                    CommandTimeout = 120
                };

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        var paramValue = param.Value;

                        if (paramValue is JsonElement jsonElement)
                            paramValue = jsonElement.ToString();
                        else if (paramValue is DateTime dt)
                            paramValue = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        else if (paramValue is bool b)
                            paramValue = b ? 1 : 0;
                        else if (paramValue == null)
                            paramValue = DBNull.Value;

                        command.Parameters.AddWithValue(param.Key, paramValue);
                    }
                }

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.GetValue(i);

                    results.Add(row);
                }

                LogQueryResults(results);
                return results;
            }
            catch (Exception ex)
            {
                string server = "unknown";
                try { server = new MySqlConnectionStringBuilder(_connectionString).Server; } catch { }

                _logger.LogError(ex,
                    "Error executing query: {Message} | Server={Server} | Inner={Inner}",
                    ex.Message,
                    server,
                    ex.InnerException?.Message ?? "(none)");

                throw new InvalidOperationException("An error occurred while executing the query.", ex);
            }
        }


        private async Task<MySqlConnection> OpenConnectionWithRetryAsync(CancellationToken ct = default)
        {
            Exception? last = null;

            // backoff schedule (seconds)
            var delays = new[] { 2, 5, 10, 15, 20, 30 };

            for (int attempt = 1; attempt <= delays.Length; attempt++)
            {
                try
                {
                    var conn = new MySqlConnection(_connectionString);

                    // Optional: if you want to enforce a hard open timeout even if connection string is misbehaving:
                    // using var openCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    // openCts.CancelAfter(TimeSpan.FromSeconds(12));
                    // await conn.OpenAsync(openCts.Token);

                    await conn.OpenAsync(ct);
                    return conn;
                }
                catch (Exception ex)
                {
                    last = ex;

                    var csb = new MySqlConnectionStringBuilder(_connectionString);
                    _logger.LogWarning(ex,
                        "MySQL OpenAsync failed (attempt {Attempt}/{Max}). Server={Server} Port={Port}.",
                        attempt, delays.Length, csb.Server, csb.Port);

                    if (attempt >= delays.Length)
                        break;

                    // small jitter so parallel jobs don't retry at the same moment
                    var jitterMs = Random.Shared.Next(0, 400);
                    var delay = TimeSpan.FromSeconds(delays[attempt - 1]) + TimeSpan.FromMilliseconds(jitterMs);

                    await Task.Delay(delay, ct);
                }
            }

            throw new InvalidOperationException("MySQL connection failed after retries.", last);
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
