using System.ComponentModel.DataAnnotations;

namespace DatabaseQueryAPI.Model.Scheduler
{
    public class RenameJobRequest
    {
        [Required]
        [MinLength(3)]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_()]+$",
            ErrorMessage = "Job name contains invalid characters.")]
        public string NewName { get; set; } = "";
    }
}