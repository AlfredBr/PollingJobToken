using api.Models;

namespace api.Services;

public interface IJobStore
{
    JobResult Create();
    JobResult? Get(string id);
    bool TryCancel(string id);
    void SetProcessing(string id);
    void SetCompleted(string id, object? data, string? message = null);
    void SetFailed(string id, string message);
}
