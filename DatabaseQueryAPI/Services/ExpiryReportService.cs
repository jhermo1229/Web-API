using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class ExpiryReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<ExpiryReportService> _logger;

        public ExpiryReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<ExpiryReportService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task<(byte[] ExcelBytes, string FileName, string SheetName)> BuildExcelAsync(int plantId, string receiveStatus)
        {
            var sql = @"
SELECT DISTINCT
    CONCAT(ff.firstname, ' ', ff.lastname) AS FIREFIGHTER_NAME,
    c.customer AS CUSTOMER,
    b.customer_batch_num AS BATCH_NUMBER,
    CASE b.plant_locationid_f
    WHEN 1 THEN 'KITCHENER'
    WHEN 2 THEN 'GATINEAU'
    ELSE 'UNKNOWN'
END AS LOCATION,
    CASE
        WHEN STR_TO_DATE(CONCAT(i.`year`, '-', LPAD(i.`month`, 2, '0'), '-01'), '%Y-%m-%d')
             < DATE_SUB(CURDATE(), INTERVAL 10 YEAR)
            THEN 'EXPIRED (10+ YEARS)'
        WHEN STR_TO_DATE(CONCAT(i.`year`, '-', LPAD(i.`month`, 2, '0'), '-01'), '%Y-%m-%d')
             BETWEEN DATE_SUB(CURDATE(), INTERVAL 10 YEAR)
                 AND DATE_SUB(CURDATE(), INTERVAL 9 YEAR)
            THEN 'ALMOST EXPIRED (9 YEARS)'
    END AS EXPIRY_STATUS
FROM workorder w
INNER JOIN firefighter ff ON w.firefighterid_f = ff.firefighterid_p
INNER JOIN workorder_item wi ON wi.workorderid_f = w.workorderid_p
INNER JOIN batch b ON b.batchid_p = w.batchid_f
INNER JOIN item i ON i.itemid_p = wi.itemid_f
INNER JOIN customer c ON c.customerid_p = b.customerid_f
WHERE
    b.receive_status = @ReceiveStatus
    AND STR_TO_DATE(CONCAT(i.`year`, '-', LPAD(i.`month`, 2, '0'), '-01'), '%Y-%m-%d')
        <= DATE_SUB(CURDATE(), INTERVAL 9 YEAR)
ORDER BY
    LOCATION, EXPIRY_STATUS;";


            var parameters = new Dictionary<string, object>
            {
                ["ReceiveStatus"] = receiveStatus,  // "active"             // 1
            };

            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var sheetName = $"GATINEAU_KITCHENER_EXPIRY";

            var fileName = $"ExpiryReport_{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var excelBytes = _excel.BuildExpiryOutlineExcel(rows, sheetName);
            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(int plantId, string receiveStatus, IEnumerable<string> toEmails)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(plantId, receiveStatus);

            await _email.SendEmailWithAttachmentAsync(
                toEmails: toEmails,
                subject: $"Gear Expiry Report - {sheetName}",
                body: "Attached is the gear expiry report (9+ years).",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation("ExpiryReportService completed | PlantId={PlantId} | File={FileName}", plantId, fileName);
        }
    }
}
