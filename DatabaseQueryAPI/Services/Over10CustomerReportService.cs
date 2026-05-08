namespace DatabaseQueryAPI.Services
{
    public class Over10CustomerReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<Over10CustomerReportService> _logger;

        public Over10CustomerReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<Over10CustomerReportService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task<(byte[] ExcelBytes, string FileName, string SheetName)> BuildExcelAsync(
            int customerId,
            string receiveStatus)
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
    'EXPIRED (10+ YEARS)' AS EXPIRY_STATUS
FROM workorder w
INNER JOIN firefighter ff ON w.firefighterid_f = ff.firefighterid_p
INNER JOIN workorder_item wi ON wi.workorderid_f = w.workorderid_p
INNER JOIN batch b ON b.batchid_p = w.batchid_f
INNER JOIN item i ON i.itemid_p = wi.itemid_f
INNER JOIN customer c ON c.customerid_p = b.customerid_f
WHERE
    b.receive_status = @ReceiveStatus
    AND c.customerid_p = @CustomerId
    AND STR_TO_DATE(CONCAT(i.`year`, '-', LPAD(i.`month`, 2, '0'), '-01'), '%Y-%m-%d')
        < DATE_SUB(CURDATE(), INTERVAL 10 YEAR)
ORDER BY
    LOCATION,
    FIREFIGHTER_NAME;";

            var parameters = new Dictionary<string, object>
            {
                ["ReceiveStatus"] = receiveStatus,
                ["CustomerId"] = customerId
            };

            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var sheetName = $"OVER_10_CUSTOMER_TORONTO";
            var fileName = $"Over10Years_Customer_Toronto_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var excelBytes = _excel.BuildExpiryOutlineExcel(rows, sheetName);

            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(int customerId, string receiveStatus, IEnumerable<string> toEmails)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(customerId, receiveStatus);

            await _email.SendEmailWithAttachmentAsync(
                toEmails: toEmails,
                subject: $"TORONTO - Over 10 Years Old Report",
                body: $"Attached is the over 10 years old gear report for Toronto.",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation(
                "Over10CustomerReportService(TORONTO) completed | CustomerId={CustomerId} | File={FileName}",
                customerId,
                fileName);
        }
    }
}