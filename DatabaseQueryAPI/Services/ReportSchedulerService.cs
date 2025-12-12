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

        // Prevent duplicate runs (jobName -> lastRunDate "yyyy-MM-dd")
        private readonly Dictionary<string, string> _lastRun = new();

        public ReportSchedulerService(
            ILogger<ReportSchedulerService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<List<ReportJobOptions>> jobs)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobs = jobs.Value ?? new List<ReportJobOptions>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            _logger.LogInformation("ReportSchedulerService started. Jobs loaded: {Count}", _jobs.Count);

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
                            await runner.RunGearReportEmailAsync(job.PlantId, job.ToEmail);

                            _logger.LogInformation("Job succeeded: {Job}", job.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Job failed: {Job}", job.Name);
                        }
                    }, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
