namespace DatabaseQueryAPI.Model
{
    public class ReportJobOptions
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public List<string> DaysOfWeek { get; set; } = new();
        public string Time { get; set; }
        public int PlantId { get; set; }
        public List<string> ToEmails { get; set; } = new();

        // NEW (optional)
        public string ReportType { get; set; } = "GearReport"; // "GearReport" or "WorkorderCount"
        public int DaysBack { get; set; } = 8;                // used by WorkorderCount
    }
}
