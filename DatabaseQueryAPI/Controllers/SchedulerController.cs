using DatabaseQueryAPI.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseQueryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulerController : ControllerBase
    {
        private readonly SchedulerStatusService _status;

        public SchedulerController(SchedulerStatusService status)
        {
            _status = status;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                isRunning = _status.IsRunning,
                lastJobName = _status.LastJobName,
                lastRunTime = _status.LastRunTime,
                lastMessage = _status.LastMessage,
                configuredJobCount = _status.ConfiguredJobCount
            });
        }
    }
}