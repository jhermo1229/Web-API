using DatabaseQueryAPI.Models;
using DatabaseQueryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        private readonly DatabaseService _databaseService;

        // Injecting DatabaseService to be used for database interaction
        public QueryController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // POST api/query
        [HttpGet]
        public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest queryRequest)
        {

            // Log the incoming request for debugging purposes
            Debug.WriteLine($"Received SQL Query: {queryRequest.SqlQuery}");
            try
            {
                // Basic validation to ensure the query is provided
                if (string.IsNullOrWhiteSpace(queryRequest.SqlQuery))
                {
                    return BadRequest(new { message = "SQL query cannot be empty." });
                }

                // Default parameters to an empty dictionary if none are provided
                if (queryRequest.Parameters == null)
                {
                    queryRequest.Parameters = new Dictionary<string, object>();
                }

                Debug.WriteLine($"Parameters: {string.Join(", ", queryRequest.Parameters.Select(p => $"{p.Key}: {p.Value}"))}");
                // Execute the query using the DatabaseService
                var result = await _databaseService.ExecuteQueryAsync(queryRequest.SqlQuery, queryRequest.Parameters);
                Debug.WriteLine($"after Query: {result}");
                // Return the results as JSON
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                // Return an error response with the exception message
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }
        private string FormatParameterValue(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ToString(); // Serialize the JSON element to string
            }

            // Handle other complex types here if necessary, or just return the value as a string
            return value?.ToString() ?? "null"; // Ensure you handle null values properly
        }
    }
}
