namespace PollingJobToken.Models;

public record JobResponse
{
    public JobResponse(JobResult jobStatus)
    {
        JobId = jobStatus.JobId.ToString() ?? string.Empty;
        Status = jobStatus.Status.ToString() ?? string.Empty;
        Message = jobStatus.Message?.ToString() ?? string.Empty;
    }
    public string JobId { get; init; }
    public string Status { get; init; }
    public string? Message { get; init; }
}