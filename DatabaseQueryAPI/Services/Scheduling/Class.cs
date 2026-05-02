namespace DatabaseQueryAPI.Model.Scheduler
{
    public class SchedulerJobDto
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public string TimeOfDay { get; set; } = "";
        public int PlantId { get; set; }
        public string ReportType { get; set; } = "GearReport";
        public int DaysBack { get; set; } = 1;
        public List<string> DaysOfWeek { get; set; } = new();
        public List<string> Recipients { get; set; } = new();
    }
}