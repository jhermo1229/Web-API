using DatabaseQueryAPI.Model.Scheduler;
using DatabaseQueryAPI.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseQueryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulerController : ControllerBase
    {
        private readonly SchedulerJobStore _jobStore;
        private readonly SchedulerStatusService _statusService;
        private readonly IServiceScopeFactory _scopeFactory;

        public SchedulerController(
            SchedulerJobStore jobStore,
            SchedulerStatusService statusService,
            IServiceScopeFactory scopeFactory)
        {
            _jobStore = jobStore;
            _statusService = statusService;
            _scopeFactory = scopeFactory;
        }

        [HttpGet("status")]
        public ActionResult<SchedulerStatusDto> GetStatus()
        {
            return Ok(new SchedulerStatusDto
            {
                IsRunning = _statusService.IsRunning,
                LastJobName = _statusService.LastJobName,
                LastRunTime = _statusService.LastRunTime,
                LastMessage = _statusService.LastMessage,
                ConfiguredJobCount = _statusService.ConfiguredJobCount
            });
        }

        [HttpGet("jobs")]
        public ActionResult<List<SchedulerJobDto>> GetJobs()
        {
            var jobs = _jobStore.GetAllJobs()
                .Select(j => new SchedulerJobDto
                {
                    Name = j.Name,
                    Enabled = j.Enabled,
                    TimeOfDay = j.TimeOfDay,
                    PlantId = j.PlantId,
                    DaysOfWeek = j.DaysOfWeek,
                    Recipients = j.Recipients
                })
                .ToList();

            return Ok(jobs);
        }

        [HttpGet("jobs/{name}")]
        public ActionResult<SchedulerJobDto> GetJob(string name)
        {
            var job = _jobStore.GetJobByName(name);

            if (job == null)
                return NotFound(new { message = $"Job '{name}' was not found." });

            return Ok(new SchedulerJobDto
            {
                Name = job.Name,
                Enabled = job.Enabled,
                TimeOfDay = job.TimeOfDay,
                PlantId = job.PlantId,
                DaysOfWeek = job.DaysOfWeek,
                Recipients = job.Recipients
            });
        }

        [HttpPut("jobs/{name}/enabled")]
        public IActionResult UpdateEnabled(string name, [FromBody] UpdateJobEnabledRequest request)
        {
            var success = _jobStore.UpdateEnabled(name, request.Enabled, out var message);

            if (!success)
                return NotFound(new { message });

            return Ok(new { message });
        }

        [HttpPost("jobs/{name}/recipients")]
        public IActionResult AddRecipient(string name, [FromBody] AddRecipientRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = _jobStore.AddRecipient(name, request.Email, out var message);

            if (!success)
                return BadRequest(new { message });

            return Ok(new { message });
        }

        [HttpDelete("jobs/{name}/recipients")]
        public IActionResult RemoveRecipient(string name, [FromBody] DeleteRecipientRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = _jobStore.RemoveRecipient(name, request.Email, out var message);

            if (!success)
                return BadRequest(new { message });

            return Ok(new { message });
        }

        [HttpPost("jobs/{name}/run-now")]
        public async Task<IActionResult> RunNow(string name)
        {
            var job = _jobStore.GetJobByName(name);

            if (job == null)
                return NotFound(new { message = $"Job '{name}' was not found." });

            try
            {
                _statusService.SetJobStarted(job.Name);

                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ReportJobRunner>();

                await runner.RunJobNowAsync(job);

                _statusService.SetJobSucceeded(job.Name);

                return Ok(new { message = $"Job '{job.Name}' ran successfully." });
            }
            catch (Exception ex)
            {
                _statusService.SetJobFailed(job.Name, ex.Message);
                return StatusCode(500, new { message = $"Job '{job.Name}' failed.", error = ex.Message });
            }
        }
    }
}