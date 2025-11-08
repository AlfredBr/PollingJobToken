using System.Linq;
using api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace api.Services;

public class CachedJobStore : IJobStore
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedJobStore> _logger;
    private readonly TimeSpan _absoluteLifetime = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _sliding = TimeSpan.FromMinutes(1);
    private readonly int _tombstoneLimit = 200; // prevent unbounded growth
    private readonly LinkedList<(string JobId, DateTimeOffset ExpiredAt)> _tombstones = new();
    private readonly object _tombstoneLock = new();

    public CachedJobStore(IMemoryCache cache, ILogger<CachedJobStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private MemoryCacheEntryOptions CreateOptions(CacheItemPriority priority)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _absoluteLifetime,
            SlidingExpiration = _sliding,
            Priority = priority
        };
        options.RegisterPostEvictionCallback(
            (key, value, reason, state) =>
            {
                if (key is string id)
                {
                    lock (_tombstoneLock)
                    {
                        _tombstones.AddLast((id, DateTimeOffset.UtcNow));
						while (_tombstones.Count > _tombstoneLimit)
						{
							_tombstones.RemoveFirst();
						}
                    }
                    _logger.LogDebug("Job {JobId} evicted: {Reason}", id, reason);
                }
            },
            state: null
        );
        return options;
    }

    private void ResetWithPriority(string id, JobResult job, CacheItemPriority priority)
    {
        _cache.Set(id, job, CreateOptions(priority));
    }

    public JobResult Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var job = new JobResult { JobId = id, Status = JobStatus.Pending };
        _cache.Set(id, job, CreateOptions(CacheItemPriority.High));
        return job;
    }

    public JobResult? Get(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
            return job;
        return null;
    }

    public bool TryCancel(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            if (job.Status is JobStatus.Completed or JobStatus.Failed)
                return false;
            job.Status = JobStatus.Canceled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
            return true;
        }
        return false;
    }

    public void SetProcessing(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job) && job.Status == JobStatus.Pending)
        {
            job.Status = JobStatus.Processing;
            ResetWithPriority(id, job, CacheItemPriority.NeverRemove);
        }
    }

    public void SetCompleted(string id, object? data, string? message = null)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            job.Status = JobStatus.Completed;
            job.Data = data;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
        }
    }

    public void SetFailed(string id, string message)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            job.Status = JobStatus.Failed;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
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
}
