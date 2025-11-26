using PollingJobToken.Models;
using Microsoft.Extensions.Caching.Memory;

namespace PollingJobToken.Services;

public class MemoryCacheJobStore : IJobStore
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheJobStore> _logger;
    private readonly TimeSpan _absoluteLifetime = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _sliding = TimeSpan.FromMinutes(1);
    private readonly int _tombstoneLimit = 200; // prevent unbounded growth
    private readonly LinkedList<(string JobId, DateTimeOffset ExpiredAt)> _tombstones = new();
    private readonly object _tombstoneLock = new();
    private readonly object _keysLock = new();
    private const string KeysCacheKey = "__job_keys__";

    public MemoryCacheJobStore(IMemoryCache cache, ILogger<MemoryCacheJobStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private HashSet<string> GetOrCreateKeySet()
    {
        if (!_cache.TryGetValue<HashSet<string>>(KeysCacheKey, out var keyset) || keyset is null)
        {
            keyset = new HashSet<string>();
            _cache.Set(KeysCacheKey, keyset, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        }
        return keyset;
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
                if (key is string id && id != KeysCacheKey)
                {
                    lock (_tombstoneLock)
                    {
                        _tombstones.AddLast((id, DateTimeOffset.UtcNow));
                        while (_tombstones.Count > _tombstoneLimit)
                        {
                            _tombstones.RemoveFirst();
                        }
                    }
                    lock (_keysLock)
                    {
                        var keys = GetOrCreateKeySet();
                        keys.Remove(id);
                    }
                    // Only count natural expiration (absolute/sliding or token) as expired
                    if (reason is EvictionReason.Expired or EvictionReason.TokenExpired)
                    {
                        JobStatusCounts.Instance.IncrementExpired();
                    }
                    _logger.LogInformation("Job {JobId} evicted: {Reason}", id, reason);
                }
            },
            state: null
        );
        return options;
    }

    private void ResetWithPriority(string id, JobResult job, CacheItemPriority priority)
    {
        _cache.Set(id, job, CreateOptions(priority));
        lock (_keysLock)
        {
            var keys = GetOrCreateKeySet();
            keys.Add(id);
        }
    }

    public JobResult Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var job = new JobResult { JobId = id, Status = JobStatus.Posted };
        _cache.Set(id, job, CreateOptions(CacheItemPriority.High));
        lock (_keysLock)
        {
            GetOrCreateKeySet().Add(id);
        }
        JobStatusCounts.Instance.IncrementPosted();
        return job;
    }

    public JobResult? Get(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job)) { return job; }
        return null;
    }

    public bool TryCancel(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            if (job is null) { return false; }
            if (job.Status is JobStatus.Completed or JobStatus.Failed) { return false; }
            if (job.Status is JobStatus.Processing)
            {
                JobStatusCounts.Instance.DecrementProcessing();
            }
            job.Status = JobStatus.Canceled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
            JobStatusCounts.Instance.IncrementCanceled();
            return true;
        }
        return false;
    }

    public void SetProcessing(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            if (job is null) { return; }
            if (job.Status is JobStatus.Posted)
            {
                job.Status = JobStatus.Processing;
                ResetWithPriority(id, job, CacheItemPriority.NeverRemove);
                JobStatusCounts.Instance.IncrementProcessing();
            }
        }
    }

    public void SetCompleted(string id, object? data, string? message = null)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            if (job is null) { return; }
            if (job.Status is JobStatus.Processing)
            {
                JobStatusCounts.Instance.DecrementProcessing();
            }
            job.Status = JobStatus.Completed;
            job.Data = data;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
            JobStatusCounts.Instance.IncrementCompleted();
        }
    }

    public void SetFailed(string id, string message)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job))
        {
            if (job is null) { return; }
            if (job.Status is JobStatus.Processing)
            {
                JobStatusCounts.Instance.DecrementProcessing();
            }
            job.Status = JobStatus.Failed;
            job.Message = message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            ResetWithPriority(id, job, CacheItemPriority.Normal);
            JobStatusCounts.Instance.IncrementFailed();
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

    public void PurgeJob(string id)
    {
        if (_cache.TryGetValue<JobResult>(id, out var job) && job is not null)
        {
            if (job.Status is JobStatus.Processing)
            {
                JobStatusCounts.Instance.DecrementProcessing();
            }
            _logger.LogWarning("Purging job {JobId} from memory cache", id);
            _cache.Remove(id);
            lock (_keysLock)
            {
                GetOrCreateKeySet().Remove(id);
            }
        }
    }

    public JobStatusCountsSnapshot GetStatusCounts()
    {
        return JobStatusCounts.Instance.Snapshot();
    }
}
