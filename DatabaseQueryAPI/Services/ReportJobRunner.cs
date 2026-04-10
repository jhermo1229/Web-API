using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DatabaseQueryAPI.Services;
using DatabaseQueryAPI.Model;

namespace DatabaseQueryAPI.Services.Scheduling
{
    public class ReportJobRunner
    {
        private readonly GearReportService _gearReportService;
        private readonly ILogger<ReportJobRunner> _logger;

        public ReportJobRunner(
            GearReportService gearReportService,
            ILogger<ReportJobRunner> logger)
        {
            _gearReportService = gearReportService;
            _logger = logger;
        }

        /// <summary>
        /// Runs the Gear Report and emails the Excel file.
        /// </summary>
        public async Task RunGearReportEmailAsync(int plantId, string toEmail)
        {
            const string receiveStatus = "ACTIVE";

            var runTime = DateTime.Now;
            var plantName = plantId == 1 ? "KITCHENER"
                           : plantId == 2 ? "GATINEAU"
                           : $"PLANT_{plantId}";

            _logger.LogInformation(
                "Starting GearReport job | Plant={Plant} | Email={Email} | Time={Time}",
                plantName, toEmail, runTime);

            await _gearReportService.SendEmailAsync(
                plantId: plantId,
                receiveStatus: receiveStatus,
                toEmail: toEmail
            );

            _logger.LogInformation(
                "Email SENT | Report=GearReport | Plant={Plant} | To={Email} | SentAt={Time}",
                plantName, toEmail, DateTime.Now);
        }

        /// <summary>
        /// Runs a scheduler job immediately for all recipients.
        /// Current version assumes all jobs are Gear Reports.
        /// </summary>
        public async Task RunJobNowAsync(SchedulerDbJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            foreach (var recipient in job.Recipients)
            {
                await RunGearReportEmailAsync(job.PlantId, recipient);
            }
        }
    }
}
