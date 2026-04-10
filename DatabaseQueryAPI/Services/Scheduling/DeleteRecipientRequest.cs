using System.ComponentModel.DataAnnotations;

namespace DatabaseQueryAPI.Model.Scheduler
{
    public class DeleteRecipientRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }
}