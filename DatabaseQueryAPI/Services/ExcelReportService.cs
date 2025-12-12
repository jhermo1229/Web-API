using ClosedXML.Excel;

namespace DatabaseQueryAPI.Services
{
    public class ExcelReportService
    {
        public byte[] BuildGearReportExcel(IEnumerable<IDictionary<string, object>> rows, string sheetName)
        {
            var list = rows?.ToList() ?? new List<IDictionary<string, object>>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);

            if (list.Count == 0)
            {
                ws.Cell(1, 1).Value = "No data returned.";
                using var msEmpty = new MemoryStream();
                wb.SaveAs(msEmpty);
                return msEmpty.ToArray();
            }

            // Headers from first row keys
            var headers = list[0].Keys.ToList();

            // Header row
            for (int c = 0; c < headers.Count; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            // Data rows
            for (int r = 0; r < list.Count; r++)
            {
                var row = list[r];
                for (int c = 0; c < headers.Count; c++)
                {
                    var key = headers[c];
                    row.TryGetValue(key, out var value);
                    ws.Cell(r + 2, c + 1).Value = value?.ToString() ?? "";
                }
            }

            // Style header: bold + yellow + freeze
            var headerRange = ws.Range(1, 1, 1, headers.Count);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;

            ws.SheetView.FreezeRows(1);

            // AutoFit
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
