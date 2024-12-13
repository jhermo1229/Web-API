using DatabaseQueryAPI.Model;
using DatabaseQueryAPI.Models;
using DatabaseQueryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        private readonly DatabaseService _databaseService;

        public QueryController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [HttpGet("GetEmployees")]
        public async Task<IActionResult> GetEmployees()
        {
            var queryRequest = new QueryRequest
            {
                SqlQuery = "SELECT \r\n    userid_p as Id, \r\n    COALESCE(CONCAT(firstname, ' ', lastname), '') as EmployeeName \r\nFROM user u;\r\n",
                Parameters = new Dictionary<string, object>()
            };

            try
            {
                // Execute the query
                var result = await _databaseService.ExecuteQueryAsync(queryRequest.SqlQuery, queryRequest.Parameters);

                // Return the result directly as a list of employees, without wrapping it in 'data'
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }






        // Endpoint for search results based on parameters
        [HttpGet("GetSearchResults")]
        public async Task<IActionResult> GetSearchResults([FromQuery] uint userId, [FromQuery] string status, [FromQuery] string startDate, [FromQuery] string endDate)
        {
            var queryRequest = new QueryRequest
            {
                SqlQuery = @"
SELECT 
    f.firstname, 
    f.lastname, 
    w.date_added, 
    c.customer,
    COUNT(*) OVER () AS TotalCount
FROM 
    workorder_history w
JOIN 
    workorder wo ON w.workorderid_f = wo.workorderid_p
JOIN 
    firefighter f ON wo.firefighterid_f = f.firefighterid_p
JOIN 
    batch b ON b.batchid_p = wo.batchid_f
JOIN 
    customer c ON b.customerid_f = c.customerid_p 
WHERE 
    w.status = @Status
    AND w.userid_f = @UserId
    AND w.date_added BETWEEN @StartDate AND @EndDate
ORDER BY 
    w.date_added ASC;",
                Parameters = new Dictionary<string, object>
                {
                    { "@UserId", userId },
                    { "@Status", status },
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                }
            };

            try
            {
                var result = await _databaseService.ExecuteQueryAsync(queryRequest.SqlQuery, queryRequest.Parameters);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }
    }
}
