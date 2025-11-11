using PollingJobToken.Models;
using System.Collections.Concurrent;

namespace PollingJobToken.Services;

public class ConcurrentDictionaryJobStore : IJobStore, IDisposable
{
    private readonly ConcurrentDictionary<string, JobResult> _jobs = new();

    // Cleanup configuration
    private readonly TimeSpan _absoluteLifetime = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _sweepInterval = TimeSpan.FromMinutes(1);

    // Tombstones to distinguish expired vs never existed
    private readonly LinkedList<(string JobId, DateTimeOffset ExpiredAt)> _tombstones = new();
    private readonly object _tombstoneLock = new();
    private readonly int _tombstoneLimit = 200;

    private readonly Timer _sweepTimer;
    private readonly ILogger<ConcurrentDictionaryJobStore> _logger;

    public ConcurrentDictionaryJobStore(ILogger<ConcurrentDictionaryJobStore> logger)
    {
        _logger = logger;
        // Start periodic sweep to cleanup old jobs
        _sweepTimer = new Timer(static state => ((ConcurrentDictionaryJobStore)state!).Sweep(), this, _sweepInterval, _sweepInterval);
    }

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

    public bool WasRecentlyExpired(string id)
    {
        var cutoff = DateTimeOffset.UtcNow - _absoluteLifetime;
        lock (_tombstoneLock)
        {
            return _tombstones.Any(t => t.JobId == id && t.ExpiredAt >= cutoff);
        }
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - _absoluteLifetime;

        foreach (var kvp in _jobs.ToArray())
        {
            var id = kvp.Key;
            var job = kvp.Value;
            var isTerminal = job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Canceled;
            var stamp = job.CompletedAt ?? job.CreatedAt;

            if (isTerminal && stamp <= cutoff)
            {
                if (_jobs.TryRemove(id, out _))
                {
                    lock (_tombstoneLock)
                    {
                        _tombstones.AddLast((id, now));
                        while (_tombstones.Count > _tombstoneLimit) _tombstones.RemoveFirst();
                    }
                    _logger.LogInformation("Job {JobId} expired and removed during sweep", id);
                }
            }
        }

        // Trim old tombstones
        var tombCutoff = now - _absoluteLifetime;
        lock (_tombstoneLock)
        {
            while (_tombstones.First is not null && _tombstones.First.Value.ExpiredAt < tombCutoff)
            {
                _tombstones.RemoveFirst();
            }
        }
    }

    public void PurgeJob(string id)
    {
        // Hard delete regardless of status; record tombstone if it existed.
        if (_jobs.TryRemove(id, out _))
        {
            var now = DateTimeOffset.UtcNow;
            lock (_tombstoneLock)
            {
                _tombstones.AddLast((id, now));
                while (_tombstones.Count > _tombstoneLimit) { _tombstones.RemoveFirst(); }
                _logger.LogWarning("Purging job {JobId} from concurrent store", id);
            }
        }
    }

    public void Dispose()
    {
        _sweepTimer.Dispose();
    }
}
