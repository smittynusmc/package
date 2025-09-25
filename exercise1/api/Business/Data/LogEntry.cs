// Business/Data/LogEntry.cs
using System.ComponentModel.DataAnnotations.Schema;
// Defines a lightweight row model for your Log table so every request/command/SQL action can be recorded with a correlation ID, timing, and context for end-to-end traceability.
namespace StargateAPI.Business.Data
{
    [Table("Log")]
    public class LogEntry
    {
        public long Id { get; set; }
        public DateTime UtcTs { get; set; } = DateTime.UtcNow;

        // What & where
        public string Level { get; set; } = "Info";      // Info | Warn | Error
        public string Category { get; set; } = "";       // HTTP | MediatR | EF | Dapper | App
        public string Message { get; set; } = "";

        // For stitching the whole request together
        public string? CorrelationId { get; set; }
        public string? User { get; set; }

        // Helpful context
        public string? Path { get; set; }                // e.g. "/person/John"
        public string? Operation { get; set; }           // e.g. GetPeople, UpdatePerson
        public int? ResponseCode { get; set; }           // HTTP or app code
        public double? DurationMs { get; set; }

        // Bodies / SQL (truncate to keep rows small)
        public string? Data { get; set; }                // JSON payload (req/resp summary) or SQL text
        public string? Exception { get; set; }           // full exception.ToString()
    }
}
