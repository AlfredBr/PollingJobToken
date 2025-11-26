namespace PollingJobToken.Models;

public class JobStatusCounts
{
    // Static global instance
    public static readonly JobStatusCounts Instance = new JobStatusCounts();

    // Counters
    private int _posted;
    private int _processing;
    private int _completed;
    private int _failed;
    private int _canceled;
    private int _expired;

    private JobStatusCounts() { }

    // Lifetime increments
    public void IncrementPosted() => System.Threading.Interlocked.Increment(ref _posted);
    public void IncrementCompleted() => System.Threading.Interlocked.Increment(ref _completed);
    public void IncrementFailed() => System.Threading.Interlocked.Increment(ref _failed);
    public void IncrementCanceled() => System.Threading.Interlocked.Increment(ref _canceled);
    public void IncrementExpired() => System.Threading.Interlocked.Increment(ref _expired);

    // Live counters adjustments
    public void IncrementProcessing() => System.Threading.Interlocked.Increment(ref _processing);
    public void DecrementProcessing() => System.Threading.Interlocked.Decrement(ref _processing);

    // Snapshot for API responses
    public JobStatusCountsSnapshot Snapshot()
    {
        // Read current values; Interlocked reads are not needed for int, but use local copies for consistency
        var posted = System.Threading.Volatile.Read(ref _posted);
        var processing = System.Threading.Volatile.Read(ref _processing);
        var completed = System.Threading.Volatile.Read(ref _completed);
        var failed = System.Threading.Volatile.Read(ref _failed);
        var canceled = System.Threading.Volatile.Read(ref _canceled);
        var expired = System.Threading.Volatile.Read(ref _expired);
        return new JobStatusCountsSnapshot(posted, processing, completed, failed, canceled, expired);
    }
}

// Immutable DTO used for returning counts from API
public record JobStatusCountsSnapshot(
    int Posted,
    int Processing,
    int Completed,
    int Failed,
    int Canceled,
    int Expired
);
