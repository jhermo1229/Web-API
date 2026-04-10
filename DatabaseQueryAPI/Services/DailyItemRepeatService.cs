namespace DatabaseQueryAPI.Services
{
    public class DailyItemRepeatService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<DailyItemRepeatService> _logger;

        public DailyItemRepeatService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<DailyItemRepeatService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task SendIfDataExistsAsync(int plantId, IEnumerable<string> toEmails)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var today = TimeZoneInfo.ConvertTime(DateTime.Now, tz).Date;

            var startDate = today.AddDays(-1); // yesterday 00:00
            var endDate = today;               // today 00:00

            var sql = @"SELECT
    CONCAT(f.firstname, ' ', f.lastname) AS FIREFIGHTER,
    c.customer AS CUSTOMER,
    b.customer_batch_num AS BATCH_NUMBER,
    CASE new_wi.item_categoryid_f
        WHEN 1 THEN 'COAT'
        WHEN 2 THEN 'PANTS'
        ELSE 'UNKNOWN'
    END AS ITEM_TYPE,    CASE b.plant_locationid_f
    WHEN 1 THEN 'KITCHENER'
    WHEN 2 THEN 'GATINEAU'
    ELSE 'UNKNOWN'
END AS LOCATION,
    new_wi.date_added AS NEW_DATE_ADDED,
    old_wi.date_added AS PRIOR_DATE_ADDED,
    TIMESTAMPDIFF(DAY, old_wi.date_added, new_wi.date_added) AS DAYS_BETWEEN
FROM workorder_item new_wi
JOIN workorder w
  ON w.workorderid_p = new_wi.workorderid_f
JOIN firefighter f
  ON f.firefighterid_p = w.firefighterid_f
JOIN batch b
  ON b.batchid_p = w.batchid_f
JOIN customer c
  ON c.customerid_p = b.customerid_f
JOIN item i
  ON i.itemid_p = new_wi.itemid_f
JOIN workorder_item old_wi
  ON old_wi.itemid_f = new_wi.itemid_f
 AND old_wi.workorder_itemid_p <> new_wi.workorder_itemid_p
 AND old_wi.date_added < new_wi.date_added
 AND old_wi.date_added >= DATE_SUB(new_wi.date_added, INTERVAL 3 MONTH)
 AND old_wi.date_added = (
      SELECT MAX(old2.date_added)
      FROM workorder_item old2
      WHERE old2.itemid_f = new_wi.itemid_f
        AND old2.workorder_itemid_p <> new_wi.workorder_itemid_p
        AND old2.date_added < new_wi.date_added
        AND old2.date_added >= DATE_SUB(new_wi.date_added, INTERVAL 3 MONTH)
 )
WHERE
    new_wi.itemid_f IS NOT NULL
    AND new_wi.date_added >= @StartDate
    AND new_wi.date_added <  @EndDate
    AND i.is_rental = 0
ORDER BY
    LOCATION, FIREFIGHTER;";

            var parameters = new Dictionary<string, object>
            {
                ["StartDate"] = startDate,
                ["EndDate"] = endDate
            };

            var result = await _databaseService.ExecuteQueryAsync(
                sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?.ToList()
                       ?? new List<IDictionary<string, object>>();

            // 🚫 DO NOTHING if no rows
            if (rows.Count == 0)
            {
                _logger.LogInformation(
                    "DailyItemRepeatReport: no data for {Date}, email not sent.",
                    startDate.ToString("yyyy-MM-dd"));
                return;
            }

            var sheetName = plantId == 1 ? "KITCHENER_REPEAT" : $"PLANT_{plantId}_REPEAT";
            var fileName = $"Repeat_Items_{startDate:yyyyMMdd}.xlsx";

            var excelBytes = _excel.BuildGearReportExcel(rows, sheetName);

            await _email.SendEmailWithAttachmentAsync(
                toEmails,
                subject: $"Gear within 3 months Detected ({startDate:yyyy-MM-dd})",
                body: "Attached are items that had repeat activity within the last 3 months.",
                attachmentBytes: excelBytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation(
                "DailyItemRepeatReport SENT | Date={Date} | Rows={Count}",
                startDate, rows.Count);
        }
    }
}
