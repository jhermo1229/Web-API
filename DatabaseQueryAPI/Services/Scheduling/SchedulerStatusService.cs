namespace DatabaseQueryAPI.Services.Scheduling
{
    public class SchedulerStatusService
    {
        public bool IsRunning { get; set; }
        public string? LastJobName { get; set; }
        public DateTime? LastRunTime { get; set; }
        public string? LastMessage { get; set; }
        public int ConfiguredJobCount { get; set; }
    }
}