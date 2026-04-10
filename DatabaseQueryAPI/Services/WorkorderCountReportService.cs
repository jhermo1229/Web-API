using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DatabaseQueryAPI.Services
{
    public class WorkorderCountReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<WorkorderCountReportService> _logger;

        public WorkorderCountReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<WorkorderCountReportService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task<(byte[] ExcelBytes, string FileName, string SheetName)> BuildExcelAsync(DateTime startDate, DateTime endDate)
        {
            var sql = @"
SELECT
    CASE b.plant_locationid_f
        WHEN 1 THEN 'Kitchener'
        WHEN 2 THEN 'Gatineau'
        ELSE 'Unknown'
    END AS PLANT_NAME,
    c.customer AS CUSTOMER,
    COUNT(*) AS TOTAL_WORKORDERS
FROM workorder w
INNER JOIN batch b
    ON b.batchid_p = w.batchid_f
INNER JOIN customer c
    ON c.customerid_p = b.customerid_f
WHERE
    w.date_added >= @StartDate
    AND w.date_added <  @EndDate
    AND b.receive_status <> 'deleted'
    AND w.workorder_statusid_f <> 99
GROUP BY
    b.plant_locationid_f,
    c.customer
ORDER BY
    PLANT_NAME,
    TOTAL_WORKORDERS DESC;";

            var parameters = new Dictionary<string, object>
            {
                ["StartDate"] = startDate,
                ["EndDate"] = endDate
            };

            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var sheetName = "Weekly Count";
            var fileName = $"WeeklyCountbyPlant_{startDate:yyyyMMdd}_to_{endDate.AddDays(-1):yyyy-MM-dd}.xlsx";

            var excelBytes = _excel.BuildWorkorderCountOutlineExcel(rows, sheetName);
            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(DateTime startDate, DateTime endDate, IEnumerable<string> toEmails)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(startDate, endDate);

            await _email.SendEmailWithAttachmentAsync(
                toEmails: toEmails,
                subject: $"Gear Count Weekly Report - {startDate:yyyy-MM-dd} to {endDate.AddDays(-1):yyyy-MM-dd}",
                body: "Attached is the workorder count report grouped by plant (expand/collapse).",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation(
                "WorkorderCountReportService completed | Range={Start}..{End} | File={FileName}",
                startDate, endDate, fileName);
        }
    }
}
