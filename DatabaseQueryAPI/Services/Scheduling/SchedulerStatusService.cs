namespace DatabaseQueryAPI.Services.Scheduling
{
    /// <summary>
    /// Shared runtime status for the scheduler.
    /// Updated by ReportSchedulerService and read by the API.
    /// </summary>
    public class SchedulerStatusService
    {
        private readonly object _lock = new();

        private bool _isRunning;
        private string? _lastJobName;
        private DateTime? _lastRunTime;
        private string? _lastMessage;
        private int _configuredJobCount;

        public bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
        }

        public string? LastJobName
        {
            get { lock (_lock) return _lastJobName; }
        }

        public DateTime? LastRunTime
        {
            get { lock (_lock) return _lastRunTime; }
        }

        public string? LastMessage
        {
            get { lock (_lock) return _lastMessage; }
        }

        public int ConfiguredJobCount
        {
            get { lock (_lock) return _configuredJobCount; }
        }

        public void SetStarted(string message, int configuredJobCount)
        {
            lock (_lock)
            {
                _isRunning = true;
                _lastMessage = message;
                _configuredJobCount = configuredJobCount;
            }
        }

        public void SetJobStarted(string jobName)
        {
            lock (_lock)
            {
                _lastJobName = jobName;
                _lastRunTime = DateTime.Now;
                _lastMessage = $"Running job: {jobName}";
            }
        }

        public void SetJobSucceeded(string jobName)
        {
            lock (_lock)
            {
                _lastJobName = jobName;
                _lastRunTime = DateTime.Now;
                _lastMessage = $"Job succeeded: {jobName}";
            }
        }

        public void SetJobFailed(string jobName, string errorMessage)
        {
            lock (_lock)
            {
                _lastJobName = jobName;
                _lastRunTime = DateTime.Now;
                _lastMessage = $"Job failed: {jobName} | {errorMessage}";
            }
        }

        public void SetHeartbeat(int configuredJobCount)
        {
            lock (_lock)
            {
                _configuredJobCount = configuredJobCount;
            }
        }

        public void SetStopped(string message)
        {
            lock (_lock)
            {
                _isRunning = false;
                _lastMessage = message;
            }
        }
    }
}