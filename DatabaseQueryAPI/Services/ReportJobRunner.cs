using DatabaseQueryAPI.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatabaseQueryAPI.Services.Scheduling
{
    public class ReportJobRunner
    {
        private readonly GearReportService _gearReportService;
        private readonly WorkorderCountReportService _workorderCountReportService;
        private readonly ExpiryReportService _expiryReportService;
        private readonly QaDailyReportService _qaDailyReportService;
        private readonly DailyItemRepeatService _dailyItemRepeatService;
        private readonly ILogger<ReportJobRunner> _logger;

        public ReportJobRunner(
            GearReportService gearReportService,
            WorkorderCountReportService workorderCountReportService,
            QaDailyReportService qaDailyReportService,
            ExpiryReportService expiryReportService,
            DailyItemRepeatService dailyItemRepeatService,
            ILogger<ReportJobRunner> logger)
        {
            _gearReportService = gearReportService;
            _workorderCountReportService = workorderCountReportService;
            _qaDailyReportService = qaDailyReportService;
            _expiryReportService = expiryReportService;
            _dailyItemRepeatService = dailyItemRepeatService;
            _logger = logger;
        }

        public async Task RunGearReportEmailAsync(int plantId, IEnumerable<string> toEmails)
        {
            const string receiveStatus = "ACTIVE";

            var runTime = DateTime.Now;
            var plantName = plantId == 1 ? "KITCHENER"
                           : plantId == 2 ? "GATINEAU"
                           : $"PLANT_{plantId}";

            _logger.LogInformation(
                "Starting GearReport job | Plant={Plant} | Email={Email} | Time={Time}",
                plantName, toEmails, runTime);

            await _gearReportService.SendEmailAsync(
                plantId: plantId,
                receiveStatus: receiveStatus,
                toEmails: toEmails
            );

            _logger.LogInformation(
                "Email SENT | Report=GearReport | Plant={Plant} | To={Email} | SentAt={Time}",
                plantName, toEmails, DateTime.Now);
        }

        // NEW
        public async Task RunWorkorderCountEmailAsync(IEnumerable<string> toEmails, int daysBack)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.Now, tz);

            // Range: [today - daysBack, today)
            var endDate = nowLocal.Date;
            var startDate = endDate.AddDays(-daysBack);

            _logger.LogInformation(
                "Starting WorkorderCount job | Range={Start}..{End} | DaysBack={DaysBack} | To={To}",
                startDate, endDate, daysBack, toEmails);

            await _workorderCountReportService.SendEmailAsync(startDate, endDate, toEmails);

            _logger.LogInformation(
                "Email SENT | Report=WorkorderCount | Range={Start}..{End} | SentAt={Time}",
                startDate, endDate, DateTime.Now);
        }

        private static DateTime GetPreviousBusinessDay(DateTime date)
        {
            // date = "run day" in local time
            var d = date.Date;

            // If Monday => go back to Friday
            if (d.DayOfWeek == DayOfWeek.Monday) return d.AddDays(-3);

            // If Sunday => go back to Friday (but we will skip weekends anyway)
            if (d.DayOfWeek == DayOfWeek.Sunday) return d.AddDays(-2);

            // If Saturday => go back to Friday (but we will skip weekends anyway)
            if (d.DayOfWeek == DayOfWeek.Saturday) return d.AddDays(-1);

            // Tue-Fri => yesterday
            return d.AddDays(-1);
        }

        public async Task RunDailyQaEmailAsync(int plantId, IEnumerable<string> toEmails)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.Now, tz);

            // Do not run on weekends
            if (nowLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                _logger.LogInformation("Daily QA report skipped (weekend).");
                return;
            }

            // Report date = previous business day (Mon => Fri)
            var reportDay = GetPreviousBusinessDay(nowLocal);

            var startDate = reportDay.Date;
            var endDate = reportDay.Date.AddDays(1);

            _logger.LogInformation(
                "Starting Daily QA job | PlantId={PlantId} | ReportDay={ReportDay} | To={ToEmails}",
                plantId, reportDay.ToString("yyyy-MM-dd"), toEmails);

            // Resolve service via DI (same pattern as other reports)
            // NOTE: this runner currently only has GearReportService injected in your earlier code.
            // So either inject DailyQaReportService into ReportJobRunner, or resolve it in scheduler scope.
            // Best match with your current pattern: inject it here like GearReportService.

            await _qaDailyReportService.SendEmailAsync(plantId, startDate, endDate, toEmails);

            _logger.LogInformation("Daily QA job SENT | PlantId={PlantId} | ReportDay={ReportDay}", plantId, reportDay.ToString("yyyy-MM-dd"));
        }

        public async Task RunExpiryReportEmailAsync(int plantId, IEnumerable<string> toEmails)
        {
            const string receiveStatus = "active";
            await _expiryReportService.SendEmailAsync(plantId, receiveStatus, toEmails);
        }
        public async Task RunDailyItemRepeatIfAnyAsync(int plantId, IEnumerable<string> toEmails)
        {
            await _dailyItemRepeatService.SendIfDataExistsAsync(plantId, toEmails);
        }


    }
}
