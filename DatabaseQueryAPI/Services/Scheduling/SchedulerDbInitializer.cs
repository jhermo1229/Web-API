using DatabaseQueryAPI.Model;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DatabaseQueryAPI.Services.Scheduling
{
    /// <summary>
    /// Creates the SQLite database and seeds jobs from appsettings.json on first run.
    /// After the first seed, SQLite becomes the permanent source of truth.
    /// </summary>
    public class SchedulerDbInitializer
    {
        private readonly string _connectionString;
        private readonly List<ReportJobOptions> _seedJobs;
        private readonly ILogger<SchedulerDbInitializer> _logger;

        public SchedulerDbInitializer(
            ILogger<SchedulerDbInitializer> logger,
            IOptions<List<ReportJobOptions>> jobOptions)
        {
            _logger = logger;

            var dbPath = Path.Combine(AppContext.BaseDirectory, "scheduler.db");
            _connectionString = $"Data Source={dbPath}";

            _seedJobs = jobOptions.Value ?? new List<ReportJobOptions>();
        }

        public void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            CreateTables(connection);
            SeedJobsIfEmpty(connection);

            _logger.LogInformation("Scheduler SQLite database initialized.");
        }

        private void CreateTables(SqliteConnection connection)
        {
            var sql = @"
CREATE TABLE IF NOT EXISTS ReportJobs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Enabled INTEGER NOT NULL DEFAULT 1,
    TimeOfDay TEXT NOT NULL,
    PlantId INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ReportJobDays (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReportJobId INTEGER NOT NULL,
    DayOfWeek TEXT NOT NULL,
    FOREIGN KEY (ReportJobId) REFERENCES ReportJobs(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ReportJobRecipients (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReportJobId INTEGER NOT NULL,
    Email TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (ReportJobId) REFERENCES ReportJobs(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ReportRunHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReportJobId INTEGER NOT NULL,
    StartedAt TEXT NOT NULL,
    FinishedAt TEXT,
    Status TEXT NOT NULL,
    Message TEXT,
    TriggeredBy TEXT,
    RecipientSnapshot TEXT,
    FOREIGN KEY (ReportJobId) REFERENCES ReportJobs(Id)
);";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private void SeedJobsIfEmpty(SqliteConnection connection)
        {
            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM ReportJobs;";
            var count = Convert.ToInt32(countCommand.ExecuteScalar());

            if (count > 0)
            {
                _logger.LogInformation("ReportJobs table already contains data. Skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding scheduler jobs from appsettings.json...");

            foreach (var job in _seedJobs)
            {
                var now = DateTime.UtcNow.ToString("o");

                using var insertJob = connection.CreateCommand();
                insertJob.CommandText = @"
INSERT INTO ReportJobs (Name, Enabled, TimeOfDay, PlantId, CreatedAt, UpdatedAt)
VALUES ($name, $enabled, $timeOfDay, $plantId, $createdAt, $updatedAt);
SELECT last_insert_rowid();";
                insertJob.Parameters.AddWithValue("$name", job.Name);
                insertJob.Parameters.AddWithValue("$enabled", job.Enabled ? 1 : 0);
                insertJob.Parameters.AddWithValue("$timeOfDay", job.Time);
                insertJob.Parameters.AddWithValue("$plantId", job.PlantId);
                insertJob.Parameters.AddWithValue("$createdAt", now);
                insertJob.Parameters.AddWithValue("$updatedAt", now);

                var reportJobId = Convert.ToInt32(insertJob.ExecuteScalar());

                foreach (var day in job.DaysOfWeek ?? new List<string>())
                {
                    using var insertDay = connection.CreateCommand();
                    insertDay.CommandText = @"
INSERT INTO ReportJobDays (ReportJobId, DayOfWeek)
VALUES ($reportJobId, $dayOfWeek);";
                    insertDay.Parameters.AddWithValue("$reportJobId", reportJobId);
                    insertDay.Parameters.AddWithValue("$dayOfWeek", day);
                    insertDay.ExecuteNonQuery();
                }

                if (!string.IsNullOrWhiteSpace(job.ToEmail))
                {
                    using var insertRecipient = connection.CreateCommand();
                    insertRecipient.CommandText = @"
INSERT INTO ReportJobRecipients (ReportJobId, Email, IsActive, CreatedAt)
VALUES ($reportJobId, $email, 1, $createdAt);";
                    insertRecipient.Parameters.AddWithValue("$reportJobId", reportJobId);
                    insertRecipient.Parameters.AddWithValue("$email", job.ToEmail.Trim());
                    insertRecipient.Parameters.AddWithValue("$createdAt", now);
                    insertRecipient.ExecuteNonQuery();
                }
            }

            _logger.LogInformation("Scheduler jobs seeded successfully.");
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}