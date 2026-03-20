using DatabaseQueryAPI.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DatabaseQueryAPI.Services.Scheduling
{
    public class ReportSchedulerService : BackgroundService
    {
        private readonly ILogger<ReportSchedulerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly List<ReportJobOptions> _jobs;
        private readonly SchedulerStatusService _status;

        // Prevent duplicate runs (jobName -> lastRunDate "yyyy-MM-dd")
        private readonly Dictionary<string, string> _lastRun = new();

        public ReportSchedulerService(
            ILogger<ReportSchedulerService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<List<ReportJobOptions>> jobs,
            SchedulerStatusService status)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobs = jobs.Value ?? new List<ReportJobOptions>();
            _status = status;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _status.IsRunning = false;
            _status.LastMessage = "Scheduler stopped.";
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            _logger.LogInformation("ReportSchedulerService started. Jobs loaded: {Count}", _jobs.Count);
            _status.IsRunning = true;
            _status.ConfiguredJobCount = _jobs.Count;
            _status.LastMessage = "Scheduler started.";

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowLocal = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
                var todayKey = nowLocal.ToString("yyyy-MM-dd");
                var dayName = nowLocal.DayOfWeek.ToString();     // e.g. "Friday"
                var timeNow = nowLocal.ToString("HH:mm");        // e.g. "15:00"

                foreach (var job in _jobs.Where(j => j.Enabled))
                {
                    if (!job.DaysOfWeek.Contains(dayName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (!string.Equals(job.Time, timeNow, StringComparison.Ordinal))
                        continue;

                    var lastKey = _lastRun.TryGetValue(job.Name, out var lr) ? lr : "";
                    if (lastKey == todayKey) // already ran today
                        continue;

                    _lastRun[job.Name] = todayKey;

                    _ = Task.Run(async () =>
                    {
                        try
                        {

                            _logger.LogInformation("Running job: {Job}", job.Name);
                            using var scope = _scopeFactory.CreateScope();

                            var runner = scope.ServiceProvider.GetRequiredService<ReportJobRunner>();

                            _status.LastJobName = job.Name;
                            _status.LastRunTime = nowLocal;
                            _status.LastMessage = $"Running job: {job.Name}";

                            if (string.Equals(job.ReportType, "WorkorderCount", StringComparison.OrdinalIgnoreCase))
                            {
                                await runner.RunWorkorderCountEmailAsync(job.ToEmails, job.DaysBack);
                            }
                            else if (string.Equals(job.ReportType, "DailyQA", StringComparison.OrdinalIgnoreCase))
                            {
                                await runner.RunDailyQaEmailAsync(job.PlantId, job.ToEmails);
                            }
                            else if (string.Equals(job.ReportType, "ExpiryReport", StringComparison.OrdinalIgnoreCase))
                            {
                                await runner.RunExpiryReportEmailAsync(job.PlantId, job.ToEmails);
                            }
                            else if (string.Equals(job.ReportType, "DailyItemRepeat", StringComparison.OrdinalIgnoreCase))
                            {
                                await runner.RunDailyItemRepeatIfAnyAsync(job.PlantId, job.ToEmails);
                            }

                            else
                            {
                                // default = GearReport (keeps existing jobs working)
                                await runner.RunGearReportEmailAsync(job.PlantId, job.ToEmails);
                            }

                            _logger.LogInformation("Job succeeded: {Job}", job.Name);
                            _status.LastMessage = $"Job succeeded: {job.Name}";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Job failed: {Job}", job.Name);
                            _status.LastMessage = $"Job failed: {job.Name}";

                            try
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                                // Send alert to you (use a config value, example below)
                                var alertTo = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Email:AlertToEmail"]
                                              ?? "jeff@sanigear.ca";
                                

                                await email.SendEmailWithAttachmentAsync(
                                    toEmails: new[] { alertTo },
                                    subject: $"Sani Gear Scheduler FAILED: {job.Name}",
                                    body: $"Job '{job.Name}' failed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.\n\n{ex}",
                                    attachmentBytes: null,
                                    attachmentFileName: null
                                );
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, "Failed to send failure alert email.");
                            }
                        }
                    }, stoppingToken);
                }

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
