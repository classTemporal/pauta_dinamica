using System;
using System.Collections.Generic;

namespace PautaDinamicaApp.Models
{
    public class AuditEntry
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
    }
}
