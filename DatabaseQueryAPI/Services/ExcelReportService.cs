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
        public byte[] BuildWorkorderCountOutlineExcel(IEnumerable<IDictionary<string, object>> rows, string sheetName)
        {
            var list = rows?.ToList() ?? new List<IDictionary<string, object>>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);

            // Define headers
            var headers = new[] { "PLANT_NAME", "CUSTOMER", "TOTAL_WORKORDERS" };

            // Header row
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            // Style header
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            ws.SheetView.FreezeRows(1);

            if (list.Count == 0)
            {
                ws.Cell(2, 1).Value = "No data returned.";
                ws.Columns().AdjustToContents();

                using var msEmpty = new MemoryStream();
                wb.SaveAs(msEmpty);
                return msEmpty.ToArray();
            }

            // Convert to typed rows
            var typed = list.Select(r => new
            {
                Plant = r.TryGetValue("PLANT_NAME", out var p) ? (p?.ToString() ?? "") : "",
                Customer = r.TryGetValue("CUSTOMER", out var c) ? (c?.ToString() ?? "") : "",
                Total = r.TryGetValue("TOTAL_WORKORDERS", out var t) && int.TryParse(t?.ToString(), out var n) ? n : 0
            }).ToList();
            var grandTotal = typed.Sum(x => x.Total);


            int row = 2;

            // Group by plant
            foreach (var plantGroup in typed.GroupBy(x => x.Plant))
            {
                var plantName = string.IsNullOrWhiteSpace(plantGroup.Key) ? "Unknown" : plantGroup.Key;
                var plantTotal = plantGroup.Sum(x => x.Total);

                // Plant summary row (level 1)
                ws.Cell(row, 1).Value = plantName;
                ws.Cell(row, 2).Value = "TOTAL";
                ws.Cell(row, 3).Value = plantTotal;
                ws.Row(row).Style.Font.Bold = true;

                var plantHeaderRow = row;
                row++;

                // Customer detail rows (level 2)
                var detailStart = row;

                foreach (var item in plantGroup.OrderByDescending(x => x.Total))
                {
                    ws.Cell(row, 1).Value = plantName;
                    ws.Cell(row, 2).Value = item.Customer;
                    ws.Cell(row, 3).Value = item.Total;
                    row++;
                }

                var detailEnd = row - 1;

                // Apply Excel outline grouping (plus/minus)
                if (detailEnd >= detailStart)
                {
                    ws.Rows(detailStart, detailEnd).Group();
                    ws.Rows(detailStart, detailEnd).Hide();
                }

                // Spacer row (optional)
                row++;
            }
            // GRAND TOTAL (ONCE, AFTER ALL PLANTS)
            ws.Cell(row, 1).Value = "GRAND TOTAL";
            ws.Cell(row, 1).Style.Font.Bold = true;

            ws.Cell(row, 3).Value = grandTotal;
            ws.Cell(row, 3).Style.Font.Bold = true;


            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public byte[] BuildDailyQaByUserOutlineExcel(
     IEnumerable<IDictionary<string, object>> rows,
     string sheetName)
        {
            var list = rows?.ToList() ?? new List<IDictionary<string, object>>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);

            ws.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;

            // Headers
            ws.Cell(1, 1).Value = "USER";
            ws.Cell(1, 2).Value = "TOTAL QA";
            ws.Cell(1, 3).Value = "FIREFIGHTER";
            ws.Cell(1, 4).Value = "QA COMPLETED AT";
            ws.Cell(1, 5).Value = "CUSTOMER";

            var header = ws.Range(1, 1, 1, 5);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.Yellow;
            ws.SheetView.FreezeRows(1);

            if (list.Count == 0)
            {
                ws.Cell(2, 1).Value = "No data returned.";
                using var msEmpty = new MemoryStream();
                wb.SaveAs(msEmpty);
                return msEmpty.ToArray();
            }

            static string GetString(IDictionary<string, object> r, params string[] keys)
            {
                foreach (var k in keys)
                {
                    var match = r.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(match.Key))
                        return match.Value?.ToString()?.Trim() ?? "";
                }
                return "";
            }

            static DateTime? GetDate(IDictionary<string, object> r, params string[] keys)
            {
                var s = GetString(r, keys);
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (DateTime.TryParse(s, out var dt)) return dt;
                return null;
            }

            // Map rows (match your SQL aliases)
            var typed = list.Select(r => new
            {
                User = GetString(r, "QA_USER_NAME", "USER_FULL_NAME", "USER"),
                Firefighter = GetString(r, "FIREFIGHTER_NAME", "FIREFIGHTER_FULL_NAME", "FIREFIGHTER"),
                Date = GetDate(r, "QA_DATE_ADDED", "QA_COMPLETED_AT", "DATE_ADDED", "DATE"),
                Customer = GetString(r, "CUSTOMER")
            }).ToList();

            int row = 2;
            int grandTotal = 0;

            foreach (var userGroup in typed
                         .GroupBy(x => string.IsNullOrWhiteSpace(x.User) ? "Unknown User" : x.User)
                         .OrderBy(g => g.Key))
            {
                var userName = userGroup.Key;
                var userTotal = userGroup.Count();
                grandTotal += userTotal;

                // User summary row (visible)
                ws.Cell(row, 1).Value = userName;
                ws.Cell(row, 2).Value = userTotal;
                ws.Row(row).Style.Font.Bold = true;
                row++;

                // Detail rows (collapsed)
                int detailStart = row;

                foreach (var item in userGroup.OrderBy(x => x.Date ?? DateTime.MinValue))
                {
                    ws.Cell(row, 3).Value = item.Firefighter;
                    ws.Cell(row, 4).Value = item.Date?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                    ws.Cell(row, 5).Value = item.Customer;
                    row++;
                }

                int detailEnd = row - 1;

                if (detailEnd >= detailStart)
                {
                    ws.Rows(detailStart, detailEnd).Group();
                    ws.Rows(detailStart, detailEnd).Hide(); // collapsed by default
                }

                row++; // spacer
            }

            // GRAND TOTAL
            ws.Cell(row, 1).Value = "GRAND TOTAL";
            ws.Cell(row, 2).Value = grandTotal;
            ws.Row(row).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public byte[] BuildExpiryOutlineExcel(IEnumerable<IDictionary<string, object>> rows, string sheetName)
        {
            var list = rows?.ToList() ?? new List<IDictionary<string, object>>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);

            ws.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;

            // Headers: summary uses EXPIRY_STATUS + TOTAL, details show CUSTOMER/BATCH/FIREFIGHTER
            var headers = new[] { "EXPIRY_STATUS", "TOTAL", "CUSTOMER", "BATCH_NUMBER", "FIREFIGHTER_NAME", "LOCATION" };

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            ws.SheetView.FreezeRows(1);

            if (list.Count == 0)
            {
                ws.Cell(2, 1).Value = "No data returned.";
                ws.Columns().AdjustToContents();

                using var msEmpty = new MemoryStream();
                wb.SaveAs(msEmpty);
                return msEmpty.ToArray();
            }

            static string GetString(IDictionary<string, object> r, params string[] keys)
            {
                foreach (var k in keys)
                {
                    var match = r.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(match.Key))
                        return match.Value?.ToString()?.Trim() ?? "";
                }
                return "";
            }

            // Map rows (match your SQL aliases)
            var typed = list.Select(r => new
            {
                Status = GetString(r, "EXPIRY_STATUS"),
                Customer = GetString(r, "CUSTOMER"),
                Batch = GetString(r, "BATCH_NUMBER"),
                Firefighter = GetString(r, "FIREFIGHTER_NAME"),
                Location = GetString(r, "LOCATION")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Status))
            .ToList();

            int row = 2;

            // Put 9-year section first, then 10+ section
            int StatusSort(string s)
            {
                if (s.Contains("ALMOST EXPIRED", StringComparison.OrdinalIgnoreCase)) return 1;
                if (s.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase)) return 2;
                return 99;
            }

            foreach (var statusGroup in typed
                         .GroupBy(x => x.Status)
                         .OrderBy(g => StatusSort(g.Key)))
            {
                var status = statusGroup.Key;
                var total = statusGroup.Count();

                // Summary row (visible)
                ws.Cell(row, 1).Value = status;
                ws.Cell(row, 2).Value = total;
                ws.Row(row).Style.Font.Bold = true;
                row++;

                // Detail rows (collapsed)
                int detailStart = row;

                foreach (var item in statusGroup
                            .OrderBy(x => x.Location)
                             .ThenBy(x => x.Customer)
                             .ThenBy(x => x.Batch)
                             .ThenBy(x => x.Firefighter))
                {
                    ws.Cell(row, 3).Value = item.Customer;
                    ws.Cell(row, 4).Value = item.Batch;
                    ws.Cell(row, 5).Value = item.Firefighter;
                    ws.Cell(row, 6).Value = item.Location;
                    row++;
                }

                int detailEnd = row - 1;

                if (detailEnd >= detailStart)
                {
                    ws.Rows(detailStart, detailEnd).Group();
                    ws.Rows(detailStart, detailEnd).Hide(); // collapsed until + clicked
                }

                row++; // spacer
            }

            // GRAND TOTAL (optional but useful)
            ws.Cell(row, 1).Value = "GRAND TOTAL";
            ws.Cell(row, 2).Value = typed.Count;
            ws.Row(row).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }



    }

}

