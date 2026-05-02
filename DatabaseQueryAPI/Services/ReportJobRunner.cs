using DatabaseQueryAPI.Model;
using DatabaseQueryAPI.Services;
using Microsoft.Extensions.Logging;

namespace DatabaseQueryAPI.Services.Scheduling
{
    public class ReportJobRunner
    {
        private readonly GearReportService _gearReportService;
        private readonly QaDailyReportService _qaDailyReportService;
        private readonly WorkorderCountReportService _workorderCountReportService;
        private readonly ExpiryReportService _expiryReportService;
        private readonly DailyItemRepeatService _dailyItemRepeatService;
        private readonly ILogger<ReportJobRunner> _logger;

        public ReportJobRunner(
            GearReportService gearReportService,
            QaDailyReportService qaDailyReportService,
            WorkorderCountReportService workorderCountReportService,
            ExpiryReportService expiryReportService,
            DailyItemRepeatService dailyItemRepeatService,
            ILogger<ReportJobRunner> logger)
        {
            _gearReportService = gearReportService;
            _qaDailyReportService = qaDailyReportService;
            _workorderCountReportService = workorderCountReportService;
            _expiryReportService = expiryReportService;
            _dailyItemRepeatService = dailyItemRepeatService;
            _logger = logger;
        }

        public async Task RunJobNowAsync(SchedulerDbJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (job.Recipients == null || job.Recipients.Count == 0)
                throw new InvalidOperationException($"Job '{job.Name}' has no recipients.");

            switch (job.ReportType)
            {
                case "GearReport":
                    await RunGearReportAsync(job);
                    break;

                case "QaDaily":
                    await RunQaDailyAsync(job);
                    break;

                case "WorkorderCount":
                    await RunWorkorderCountAsync(job);
                    break;

                case "ExpiryReport":
                    await RunExpiryReportAsync(job);
                    break;

                case "ItemRepeat":
                    await RunItemRepeatAsync(job);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown report type: {job.ReportType}");
            }
        }

        private async Task RunGearReportAsync(SchedulerDbJob job)
        {
            const string receiveStatus = "ACTIVE";

            foreach (var recipient in job.Recipients)
            {
                _logger.LogInformation(
                    "Starting GearReport job | Job={Job} | PlantId={PlantId} | Email={Email}",
                    job.Name, job.PlantId, recipient);

                await _gearReportService.SendEmailAsync(
                    plantId: job.PlantId,
                    receiveStatus: receiveStatus,
                    toEmails: new[] { recipient }
                );
            }
        }

        private async Task RunQaDailyAsync(SchedulerDbJob job)
        {
            var reportDate = GetPreviousBusinessDay(DateTime.Today);
            var startDate = reportDate.Date;
            var endDate = startDate.AddDays(1);

            _logger.LogInformation(
                "Starting QA Daily job | Job={Job} | PlantId={PlantId} | Date={Date}",
                job.Name, job.PlantId, startDate.ToString("yyyy-MM-dd"));

            await _qaDailyReportService.SendEmailAsync(
                plantId: job.PlantId,
                startDate: startDate,
                endDate: endDate,
                toEmails: job.Recipients
            );
        }

        private static DateTime GetPreviousBusinessDay(DateTime today)
        {
            return today.DayOfWeek switch
            {
                DayOfWeek.Monday => today.AddDays(-3),
                DayOfWeek.Sunday => today.AddDays(-2),
                _ => today.AddDays(-1)
            };
        }

        private async Task RunWorkorderCountAsync(SchedulerDbJob job)
        {
            var daysBack = job.DaysBack <= 0 ? 7 : job.DaysBack;

            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-daysBack);

            _logger.LogInformation(
                "Starting WorkorderCount job | Job={Job} | Range={Start} to {End}",
                job.Name,
                startDate.ToString("yyyy-MM-dd"),
                endDate.AddDays(-1).ToString("yyyy-MM-dd"));

            await _workorderCountReportService.SendEmailAsync(
                startDate: startDate,
                endDate: endDate,
                toEmails: job.Recipients
            );
        }

        private async Task RunExpiryReportAsync(SchedulerDbJob job)
        {
            const string receiveStatus = "active";

            _logger.LogInformation(
                "Starting ExpiryReport job | Job={Job} | PlantId={PlantId}",
                job.Name,
                job.PlantId);

            await _expiryReportService.SendEmailAsync(
                plantId: job.PlantId,
                receiveStatus: receiveStatus,
                toEmails: job.Recipients
            );
        }

        private async Task RunItemRepeatAsync(SchedulerDbJob job)
        {
            _logger.LogInformation(
                "Starting ItemRepeat job | Job={Job} | PlantId={PlantId}",
                job.Name,
                job.PlantId);

            await _dailyItemRepeatService.SendIfDataExistsAsync(
                plantId: job.PlantId,
                toEmails: job.Recipients
            );
        }
    }
}