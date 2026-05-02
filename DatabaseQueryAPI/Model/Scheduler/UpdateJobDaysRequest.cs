using System.ComponentModel.DataAnnotations;

namespace DatabaseQueryAPI.Model.Scheduler
{
    public class UpdateJobDaysRequest
    {
        [Required]
        public List<string> DaysOfWeek { get; set; } = new();
    }
}