namespace api.Services;

public interface IJobProcessor<TRequest, TResult>
{
    Task<TResult> RunAsync(TRequest request, CancellationToken cancellationToken);
}
