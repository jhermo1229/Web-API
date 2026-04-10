using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DatabaseQueryAPI.Services.Scheduling
{
    public class ReportSchedulerService : BackgroundService
    {
        private readonly ILogger<ReportSchedulerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulerJobStore _jobStore;
        private readonly SchedulerStatusService _statusService;

        // Prevent duplicate runs (jobName -> lastRunDate "yyyy-MM-dd")
        private readonly Dictionary<string, string> _lastRun = new();

        public ReportSchedulerService(
            ILogger<ReportSchedulerService> logger,
            IServiceScopeFactory scopeFactory,
            SchedulerJobStore jobStore,
            SchedulerStatusService statusService)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobStore = jobStore;
            _statusService = statusService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            _logger.LogInformation("ReportSchedulerService started.");

            var startupJobCount = _jobStore.GetAllJobs().Count;
            _statusService.SetStarted("Scheduler started.", startupJobCount);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var nowLocal = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
                    var todayKey = nowLocal.ToString("yyyy-MM-dd");
                    var dayName = nowLocal.DayOfWeek.ToString();
                    var timeNow = nowLocal.ToString("HH:mm");

                    var jobs = _jobStore.GetAllJobs();
                    _statusService.SetHeartbeat(jobs.Count);

                    foreach (var job in jobs.Where(j => j.Enabled))
                    {
                        if (!job.DaysOfWeek.Contains(dayName, StringComparer.OrdinalIgnoreCase))
                            continue;

                        if (!string.Equals(job.TimeOfDay, timeNow, StringComparison.Ordinal))
                            continue;

                        var lastKey = _lastRun.TryGetValue(job.Name, out var lr) ? lr : "";
                        if (lastKey == todayKey)
                            continue;

                        _lastRun[job.Name] = todayKey;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                _logger.LogInformation("Running job: {Job}", job.Name);
                                _statusService.SetJobStarted(job.Name);

                                using var scope = _scopeFactory.CreateScope();
                                var runner = scope.ServiceProvider.GetRequiredService<ReportJobRunner>();

                                foreach (var recipient in job.Recipients)
                                {
                                    await runner.RunGearReportEmailAsync(job.PlantId, recipient);
                                }

                                _logger.LogInformation("Job succeeded: {Job}", job.Name);
                                _statusService.SetJobSucceeded(job.Name);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Job failed: {Job}", job.Name);
                                _statusService.SetJobFailed(job.Name, ex.Message);
                            }
                        }, stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            finally
            {
                _statusService.SetStopped("Scheduler stopped.");
            }
        }
    }
}