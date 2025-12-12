using DatabaseQueryAPI.Services.Scheduling;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services
{
    public class GearReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelReportService _excel;
        private readonly EmailService _email;
        private readonly ILogger<GearReportService> _logger;

        public GearReportService(
            DatabaseService databaseService,
            ExcelReportService excel,
            EmailService email,
            ILogger<GearReportService> logger)
        {
            _databaseService = databaseService;
            _excel = excel;
            _email = email;
            _logger = logger;
        }

        public async Task<(byte[] ExcelBytes, string FileName, string SheetName)> BuildExcelAsync(int plantId, string receiveStatus)
        {
            var sql = @"
SELECT
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
    AND b.plant_locationid_f = @PlantId
ORDER BY CUSTOMER;";

            var parameters = new Dictionary<string, object>
            {
                ["ReceiveStatus"] = receiveStatus,
                ["PlantId"] = plantId
            };

            // No HttpContext here — pass something stable
            var result = await _databaseService.ExecuteQueryAsync(sql, parameters, "Scheduler/Controller", "LOCAL");

            var rows = (result as IEnumerable<IDictionary<string, object>>)
                       ?? throw new Exception("ExecuteQueryAsync did not return a dictionary rowset.");

            var sheetName = plantId == 1 ? "KITCHENER" : plantId == 2 ? "GATINEAU" : $"PLANT_{plantId}";
            var fileName = $"GearReport_{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var excelBytes = _excel.BuildGearReportExcel(rows, sheetName);
            return (excelBytes, fileName, sheetName);
        }

        public async Task SendEmailAsync(int plantId, string receiveStatus, string toEmail)
        {
            var (bytes, fileName, sheetName) = await BuildExcelAsync(plantId, receiveStatus);

            await _email.SendEmailWithAttachmentAsync(
                toEmail: toEmail,
                subject: $"Gear Report - {sheetName}",
                body: "Attached is the gear report.",
                attachmentBytes: bytes,
                attachmentFileName: fileName
            );

            _logger.LogInformation(
    "GearReportService completed | PlantId={PlantId} | File={FileName}",
    plantId, fileName);

        }
    }
}
