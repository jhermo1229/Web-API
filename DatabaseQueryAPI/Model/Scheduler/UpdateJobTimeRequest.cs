using System.ComponentModel.DataAnnotations;

namespace DatabaseQueryAPI.Model.Scheduler
{
    public class UpdateJobTimeRequest
    {
        [Required]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Time must be in HH:mm format.")]
        public string TimeOfDay { get; set; } = "";
    }
}