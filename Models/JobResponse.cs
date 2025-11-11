namespace PollingJobToken.Models;

public record JobResponse
{
    public JobResponse(JobResult jobStatus)
    {
        JobId = jobStatus.JobId;
        Status = jobStatus.Status;
        Message = jobStatus.Message;
    }
    public string JobId { get; init; }
    public JobStatus Status { get; init; }
    public string? Message { get; init; }
}