namespace DatabaseQueryAPI.Model.Scheduler
{
    public class SchedulerJobDto
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public string TimeOfDay { get; set; } = "";
        public int PlantId { get; set; }
        public List<string> DaysOfWeek { get; set; } = new();
        public List<string> Recipients { get; set; } = new();
    }
}