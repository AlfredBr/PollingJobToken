using System.Collections.Concurrent;
using api.Models;

namespace api.Services;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, JobResult> _jobs = new();

    public JobResult Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var job = new JobResult { JobId = id, Status = JobStatus.Pending };
        _jobs[id] = job;
        return job;
    }

    public JobResult? Get(string id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public bool TryCancel(string id)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            if (job is null) { return false; }
            if (job.Status is JobStatus.Completed or JobStatus.Failed)
			{
				return false;
			}
            job.Status = JobStatus.Canceled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            return true;
        }
        return false;
    }

    public void SetProcessing(string id)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            if (job is null) { return; }
            if (job.Status == JobStatus.Pending)
            {
                job.Status = JobStatus.Processing;
            }
        }
    }

    public void SetCompleted(string id, object? data, string? message = null)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            if (job is null) { return; }
            job.Status = JobStatus.Completed;
            job.Data = data;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    public void SetFailed(string id, string message)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            if (job is null) { return; }
            job.Status = JobStatus.Failed;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    public bool WasRecentlyExpired(string id) => false; // no eviction in dictionary implementation
}
