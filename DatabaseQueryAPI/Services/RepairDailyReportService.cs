namespace DatabaseQueryAPI.Services
{
    public class RepairDailyReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<RepairDailyReportService> _logger;

        public RepairDailyReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<RepairDailyReportService> logger)
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
    c.customer AS CUSTOMER,
    CASE
    WHEN TRIM(IFNULL(u.firstname, '')) <> ''
         AND TRIM(IFNULL(u.lastname, '')) <> ''
        THEN CONCAT(u.firstname, ' ', u.lastname)

    WHEN TRIM(IFNULL(u.firstname, '')) <> ''
        THEN u.firstname

    WHEN TRIM(IFNULL(u.lastname, '')) <> ''
        THEN u.lastname

    ELSE 'Unknown User'
END AS QA_USER_NAME
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
                ["Status"] = "Pending QA",
                ["StartDate"] = startDate,
                ["EndDate"] = endDate,
                ["PlantId"] = plantId
            };

            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var plantName = plantId == 1 ? "KITCHENER"
                          : plantId == 2 ? "GATINEAU"
                          : $"PLANT_{plantId}";

            var sheetName = $"{plantName}_REPAIR";
            var fileName = $"Repair_Daily_{plantName}_{startDate:yyyyMMdd}.xlsx";

            var excelBytes = _excel.BuildDailyQaByUserOutlineExcel(rows, sheetName);

            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(int plantId, DateTime startDate, DateTime endDate, IEnumerable<string> toEmails)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(plantId, startDate, endDate);

            await _email.SendEmailWithAttachmentAsync(
                toEmails: toEmails,
                subject: $"Repair Daily Report - {sheetName} - {startDate:yyyy-MM-dd}",
                body: $"Attached is the repair daily report for {startDate:yyyy-MM-dd}.",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation(
                "RepairDailyReportService completed | PlantId={PlantId} | File={FileName}",
                plantId,
                fileName);
        }
    }
}