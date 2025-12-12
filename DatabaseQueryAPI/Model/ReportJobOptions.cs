namespace DatabaseQueryAPI.Model
{
    public class ReportJobOptions
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public List<string> DaysOfWeek { get; set; } = new();
        public string Time { get; set; } = "15:00"; // HH:mm
        public int PlantId { get; set; }
        public string ToEmail { get; set; } = "";
    }
}
