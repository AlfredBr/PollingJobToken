using System.Text.Json.Serialization;

namespace api.Models;

public class JobResult
{
    public required string JobId { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required JobStatus Status { get; set; }
    
    public string? Message { get; set; }
    public object? Data { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
