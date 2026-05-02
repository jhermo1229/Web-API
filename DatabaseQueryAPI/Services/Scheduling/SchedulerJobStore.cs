using DatabaseQueryAPI.Model;
using Microsoft.Data.Sqlite;

namespace DatabaseQueryAPI.Services.Scheduling
{
    /// <summary>
    /// SQLite-backed job store (permanent source of truth).
    /// Scheduler reads from this every loop.
    /// API will also use this to modify jobs.
    /// </summary>
    public class SchedulerJobStore
    {
        private readonly string _connectionString;
        private readonly ILogger<SchedulerJobStore> _logger;

        public SchedulerJobStore(
            SchedulerDbInitializer dbInitializer,
            ILogger<SchedulerJobStore> logger)
        {
            _connectionString = dbInitializer.GetConnectionString();
            _logger = logger;
        }

        /// <summary>
        /// Get all jobs with days and recipients
        /// </summary>
        public List<SchedulerDbJob> GetAllJobs()
        {
            var jobs = new Dictionary<int, SchedulerDbJob>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT 
    j.Id,
    j.Name,
    j.Enabled,
    j.TimeOfDay,
    j.PlantId,
    j.ReportType,
    j.DaysBack,
    d.DayOfWeek,
    r.Email
FROM ReportJobs j
LEFT JOIN ReportJobDays d ON d.ReportJobId = j.Id
LEFT JOIN ReportJobRecipients r ON r.ReportJobId = j.Id AND r.IsActive = 1
ORDER BY j.Name;";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetInt32(0);

                // Create job object if not exists
                if (!jobs.ContainsKey(id))
                {
                    jobs[id] = new SchedulerDbJob
                    {
                        Id = id,
                        Name = reader.GetString(1),
                        Enabled = reader.GetInt32(2) == 1,
                        TimeOfDay = reader.GetString(3),
                        PlantId = reader.GetInt32(4),
                        ReportType = reader.GetString(5),
                        DaysBack = reader.GetInt32(6)
                    };
                }

                if (!reader.IsDBNull(7))
                {
                    var day = reader.GetString(7);
                    if (!jobs[id].DaysOfWeek.Contains(day, StringComparer.OrdinalIgnoreCase))
                        jobs[id].DaysOfWeek.Add(day);
                }

                if (!reader.IsDBNull(8))
                {
                    var email = reader.GetString(8);
                    if (!jobs[id].Recipients.Contains(email, StringComparer.OrdinalIgnoreCase))
                        jobs[id].Recipients.Add(email);
                }
            }

            return jobs.Values.OrderBy(j => j.Name).ToList();
        }

        /// <summary>
        /// Get a job by name
        /// </summary>
        public SchedulerDbJob? GetJobByName(string jobName)
        {
            return GetAllJobs()
                .FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get a job by ID
        /// </summary>
        public SchedulerDbJob? GetJobById(int jobId)
        {
            return GetAllJobs().FirstOrDefault(j => j.Id == jobId);
        }

        /// <summary>
        /// Enable / Disable a job
        /// </summary>
        public bool UpdateEnabled(string jobName, bool enabled, out string message)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE ReportJobs
SET Enabled = $enabled,
    UpdatedAt = $updatedAt
WHERE LOWER(Name) = LOWER($name);";

            command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$name", jobName);

            var rows = command.ExecuteNonQuery();

            if (rows == 0)
            {
                message = $"Job '{jobName}' was not found.";
                return false;
            }

            message = $"Job '{jobName}' has been {(enabled ? "enabled" : "disabled")}.";
            return true;
        }

        /// <summary>
        /// Add email recipient to a job
        /// </summary>
        public bool AddRecipient(string jobName, string email, out string message)
        {
            email = email?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(email))
            {
                message = "Email cannot be empty.";
                return false;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var jobId = GetJobId(connection, jobName);
            if (jobId == null)
            {
                message = $"Job '{jobName}' was not found.";
                return false;
            }

            // Check duplicate
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
SELECT COUNT(*)
FROM ReportJobRecipients
WHERE ReportJobId = $jobId
  AND LOWER(Email) = LOWER($email)
  AND IsActive = 1;";
            checkCmd.Parameters.AddWithValue("$jobId", jobId.Value);
            checkCmd.Parameters.AddWithValue("$email", email);

            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                message = $"Email '{email}' already exists.";
                return false;
            }

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
INSERT INTO ReportJobRecipients (ReportJobId, Email, IsActive, CreatedAt)
VALUES ($jobId, $email, 1, $createdAt);";

            insertCmd.Parameters.AddWithValue("$jobId", jobId.Value);
            insertCmd.Parameters.AddWithValue("$email", email);
            insertCmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));

            insertCmd.ExecuteNonQuery();

            message = $"Email '{email}' added.";
            return true;
        }

        /// <summary>
        /// Remove (deactivate) recipient
        /// </summary>
        public bool RemoveRecipient(string jobName, string email, out string message)
        {
            email = email?.Trim() ?? "";

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var jobId = GetJobId(connection, jobName);
            if (jobId == null)
            {
                message = $"Job '{jobName}' not found.";
                return false;
            }

            using var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE ReportJobRecipients
SET IsActive = 0
WHERE ReportJobId = $jobId
  AND LOWER(Email) = LOWER($email)
  AND IsActive = 1;";

            command.Parameters.AddWithValue("$jobId", jobId.Value);
            command.Parameters.AddWithValue("$email", email);

            var rows = command.ExecuteNonQuery();

            if (rows == 0)
            {
                message = $"Email '{email}' not found.";
                return false;
            }

            message = $"Email '{email}' removed.";
            return true;
        }

        /// <summary>
        /// Helper: get job ID by name
        /// </summary>
        private int? GetJobId(SqliteConnection connection, string jobName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT Id
FROM ReportJobs
WHERE LOWER(Name) = LOWER($name)
LIMIT 1;";

            command.Parameters.AddWithValue("$name", jobName);

            var result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt32(result);
        }

        public bool UpdateTime(string jobName, string timeOfDay, out string message)
        {
            if (string.IsNullOrWhiteSpace(timeOfDay))
            {
                message = "Time cannot be empty.";
                return false;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE ReportJobs
SET TimeOfDay = $timeOfDay,
    UpdatedAt = $updatedAt
WHERE LOWER(Name) = LOWER($name);";

            command.Parameters.AddWithValue("$timeOfDay", timeOfDay.Trim());
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$name", jobName);

            var rows = command.ExecuteNonQuery();

            if (rows == 0)
            {
                message = $"Job '{jobName}' was not found.";
                return false;
            }

            message = $"Time updated for job '{jobName}' to {timeOfDay}.";
            return true;
        }

        public bool UpdateDays(string jobName, List<string> daysOfWeek, out string message)
        {
            var validDays = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

            var cleanDays = daysOfWeek
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanDays.Count == 0)
            {
                message = "At least one day must be selected.";
                return false;
            }

            if (cleanDays.Any(d => !validDays.Contains(d)))
            {
                message = "One or more selected days are invalid.";
                return false;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                var jobId = GetJobId(connection, jobName);
                if (jobId == null)
                {
                    message = $"Job '{jobName}' was not found.";
                    return false;
                }

                using var deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM ReportJobDays WHERE ReportJobId = $jobId;";
                deleteCommand.Parameters.AddWithValue("$jobId", jobId.Value);
                deleteCommand.ExecuteNonQuery();

                foreach (var day in cleanDays)
                {
                    using var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
INSERT INTO ReportJobDays (ReportJobId, DayOfWeek)
VALUES ($jobId, $dayOfWeek);";
                    insertCommand.Parameters.AddWithValue("$jobId", jobId.Value);
                    insertCommand.Parameters.AddWithValue("$dayOfWeek", day);
                    insertCommand.ExecuteNonQuery();
                }

                using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = @"
UPDATE ReportJobs
SET UpdatedAt = $updatedAt
WHERE Id = $jobId;";
                updateCommand.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("o"));
                updateCommand.Parameters.AddWithValue("$jobId", jobId.Value);
                updateCommand.ExecuteNonQuery();

                transaction.Commit();

                message = $"Days updated for job '{jobName}'.";
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                message = ex.Message;
                return false;
            }
        }

        public bool RenameJob(string oldName, string newName, out string message)
        {
            newName = newName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(newName))
            {
                message = "New job name cannot be empty.";
                return false;
            }
            var invalidChars = new[] { '/', '\\', '?', '#', '%', '*', ':', '[', ']' };

            if (newName.Any(c => invalidChars.Contains(c)))
            {
                message = "Job name contains invalid characters: / \\ ? # % * : [ ]";
                return false;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE ReportJobs
SET Name = $newName,
    UpdatedAt = $updatedAt
WHERE LOWER(Name) = LOWER($oldName);";

            command.Parameters.AddWithValue("$newName", newName);
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$oldName", oldName);

            try
            {
                var rows = command.ExecuteNonQuery();

                if (rows == 0)
                {
                    message = $"Job '{oldName}' was not found.";
                    return false;
                }

                message = $"Job renamed to '{newName}'.";
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                message = $"A job named '{newName}' already exists.";
                return false;
            }
        }
    }

}