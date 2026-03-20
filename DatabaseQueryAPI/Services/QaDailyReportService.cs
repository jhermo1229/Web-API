using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class QaDailyReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<QaDailyReportService> _logger;

        public QaDailyReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<QaDailyReportService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task<(byte[] ExcelBytes, string FileName, string SheetName)> BuildExcelAsync(
            int plantId,
            DateTime startDate,
            DateTime endDate)
        {
            var sql = @"
SELECT
    CONCAT(f.firstname, ' ', f.lastname) AS FIREFIGHTER_NAME,
    w.date_added AS QA_DATE_ADDED,
    c.customer AS CUSTOMER,
    CONCAT(u.firstname, ' ', u.lastname) AS QA_USER_NAME,
    w.userid_f AS USER_ID
FROM workorder_history w
JOIN workorder wo ON w.workorderid_f = wo.workorderid_p
JOIN firefighter f ON wo.firefighterid_f = f.firefighterid_p
JOIN batch b ON b.batchid_p = wo.batchid_f
JOIN customer c ON b.customerid_f = c.customerid_p
JOIN user u ON u.userid_p = w.userid_f
WHERE
    w.status = @Status
    AND w.date_added >= @StartDate
    AND w.date_added <  @EndDate
    AND b.plant_locationid_f = @PlantId
    AND wo.workorder_statusid_f <> 99
    AND b.receive_status <> 'deleted'
ORDER BY
    QA_USER_NAME,
    w.date_added ASC;";

            var parameters = new Dictionary<string, object>
            {
                ["Status"] = "Completed",
                ["StartDate"] = startDate,
                ["EndDate"] = endDate,
                ["PlantId"] = plantId
            };

            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var sheetName = plantId == 1 ? "KITCHENER_QA"
                          : plantId == 2 ? "GATINEAU_QA"
                          : $"PLANT_{plantId}_QA";

            var fileName = $"QA_ByUser_{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var excelBytes = _excel.BuildDailyQaByUserOutlineExcel(rows, sheetName);
            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(int plantId, DateTime startDate, DateTime endDate, IEnumerable<string> toEmails)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(plantId, startDate, endDate);

            await _email.SendEmailWithAttachmentAsync(
                toEmails: toEmails,
                subject: $"QA Count By User (Daily) - {sheetName}",
                body: $"Attached is the QA-by-user report for {startDate:yyyy-MM-dd}.",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation("QaDailyReportService completed | PlantId={PlantId} | File={FileName}", plantId, fileName);
        }
    }
}
