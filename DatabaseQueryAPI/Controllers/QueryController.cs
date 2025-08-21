using DatabaseQueryAPI.Models;
using DatabaseQueryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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
                SqlQuery = "SELECT \r\n    userid_p as Id, \r\n    CONCAT(COALESCE(firstname, ''), ' ', COALESCE(lastname, '')) as EmployeeName  \r\nFROM user u;\r\n",
                Parameters = new Dictionary<string, object>()
            };

            // Get the client's IP address
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {
                // Execute the query and pass the IP address
                var result = await _databaseService.ExecuteQueryAsync(queryRequest.SqlQuery, queryRequest.Parameters, "Unknown", clientIp);

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

            // Get the client's IP address
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {
                // Execute the query and pass the IP address
                var result = await _databaseService.ExecuteQueryAsync(queryRequest.SqlQuery, queryRequest.Parameters, "Unknown", clientIp);

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }



        //Get shell only for active batches
        [HttpGet("GetGearReport")]
        public async Task<IActionResult> GetGearReport(
    [FromQuery] string receiveStatus = "ACTIVE")
        {
            var sql = @"
SELECT
	CASE  b.plant_locationid_f
    WHEN 1 THEN 'KITCHENER'
    WHEN 2 THEN 'GATINEAU'
    ELSE 'UNKNOWN'
  END AS PLANT,
    CONCAT(ff.firstname, ' ', ff.lastname) AS FIREFIGHTER_NAME,
    c.customer AS CUSTOMER,
    b.customer_batch_num AS BATCH_NUMBER,
    cs.StationNumber AS STATION_NUMBER,
    CASE wi.item_categoryid_f
        WHEN 1 THEN 'COAT'
        WHEN 2 THEN 'PANTS'
        ELSE 'UNKNOWN'
    END AS ITEM_TYPE,
    i.trim_barcode AS SERIAL_NUMBER,
    m.manufacturer AS MANUFACTURER,
    i.waist AS WAIST,
    i.inseam AS INSEAM,
    i.chest AS CHEST,
    i.sleeve AS SLEEVE,
    i.length AS LENGTH,
  		  CASE wi.ShellOnly
    WHEN 1 THEN 'Shell only'
	 ELSE 'Liner Only'
  END AS SHELL_LINER
FROM workorder w
INNER JOIN firefighter ff ON w.firefighterid_f = ff.firefighterid_p
INNER JOIN workorder_item wi ON wi.workorderid_f = w.workorderid_p
INNER JOIN batch b ON b.batchid_p = w.batchid_f
INNER JOIN customer c ON c.customerid_p = b.customerid_f
INNER JOIN item i ON i.itemid_p = wi.itemid_f
INNER JOIN manufacturer m ON m.manufacturerid_p = i.manufacturerid_f
LEFT JOIN customer_station cs ON cs.customer_stationid_p = w.customer_stationid_f
WHERE
b.receive_status = @ReceiveStatus
    AND (wi.ShellOnly = 1 OR wi.LinerOnly = 1)
ORDER BY PLANT, CUSTOMER;";

            var parameters = new Dictionary<string, object>
            {
                ["ReceiveStatus"] = receiveStatus,
            };

            // Get the client's IP address
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {
                var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Unknown", clientIp);
                return Ok(result); // returns the rows directly
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }

    }


}
