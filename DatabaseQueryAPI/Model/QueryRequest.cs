using System.Collections.Generic;

namespace DatabaseQueryAPI.Models
{
    public class QueryRequest
    {
        public string SqlQuery { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}
