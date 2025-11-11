namespace PollingJobToken.Models;

using System.Text.Json.Serialization;

public record JobResponse
{
    public JobResponse(JobResult jobStatus)
    {
        JobId = jobStatus.JobId;
        Status = jobStatus.Status;
        Message = jobStatus.Message;
    }
    public string JobId { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobStatus Status { get; init; }
    public string? Message { get; init; }
}