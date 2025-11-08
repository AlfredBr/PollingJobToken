using Microsoft.AspNetCore.Mvc;
using api.Services;

namespace api.Controllers;

// Base class to submit typed jobs that produce typed results
public abstract class JobSubmissionControllerBase<TRequest, TResult> : ControllerBase
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(2);
    protected readonly IJobStore _jobstore;
    protected readonly IJobProcessor<TRequest, TResult> _jobprocessor;
    protected readonly ILogger _logger;

    protected JobSubmissionControllerBase(IJobStore jobstore, IJobProcessor<TRequest, TResult> processor, ILogger logger)
    {
        _jobstore = jobstore;
        _jobprocessor = processor;
        _logger = logger;
    }

    protected ActionResult SubmitJobInternal(TRequest request)
    {
        var job = _jobstore.Create();

        _ = Task.Run(async () =>
        {
            try
            {
                _jobstore.SetProcessing(job.JobId);
                var result = await _jobprocessor.RunAsync(request, HttpContext.RequestAborted);
                _jobstore.SetCompleted(job.JobId, result, message: "Completed");
            }
            catch (OperationCanceledException)
            {
                // If server is shutting down or request aborted, mark failed (or canceled if desired)
                _jobstore.SetFailed(job.JobId, "Canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}", job.JobId);
                _jobstore.SetFailed(job.JobId, ex.Message);
            }
        });

        var location = Url.ActionLink(
            action: nameof(JobsController.GetJob), 
            controller: "Jobs", 
            values: new { id = job.JobId }) ?? $"/jobs/{job.JobId}";
        Response.Headers.RetryAfter = DefaultRetryAfter.TotalSeconds.ToString("F0");
        return Accepted(location, new { jobId = job.JobId });
    }
}
