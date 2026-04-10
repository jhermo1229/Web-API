namespace DatabaseQueryAPI.Model.Scheduler
{
    public class SchedulerStatusDto
    {
        public bool IsRunning { get; set; }
        public string? LastJobName { get; set; }
        public DateTime? LastRunTime { get; set; }
        public string? LastMessage { get; set; }
        public int ConfiguredJobCount { get; set; }
    }
}