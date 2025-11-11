using Microsoft.AspNetCore.Mvc;
using PollingJobToken.Services;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace PollingJobToken.Controllers;

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

    protected ActionResult SubmitJobInternal(TRequest request, [CallerMemberName] string callerMethodName = "")
    {
        // Get the calling class name using reflection
        var callerClassName = GetType().Name;
        var callerInfo = $"{callerClassName}.{callerMethodName}";

        // Create job token
        var job = _jobstore.Create();
        _logger.LogInformation("SubmitJob received. jobType={JobType} jobId={JobId} caller={Caller}", typeof(TRequest).Name, job.JobId, callerInfo);

        // Kick off background processing
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("Job background task starting. jobId={JobId} caller={Caller}", job.JobId, callerInfo);
            try
            {
                _jobstore.SetProcessing(job.JobId);
                _logger.LogInformation("Job status set to Processing. jobId={JobId}", job.JobId);

                var result = await _jobprocessor.RunAsync(request, HttpContext.RequestAborted);
                _logger.LogInformation("Job processor completed successfully. jobId={JobId} resultType={ResultType}", job.JobId, typeof(TResult).Name);

                _jobstore.SetCompleted(job.JobId, result, message: $"{callerInfo} Completed");
                _logger.LogInformation("Job status set to Completed. jobId={JobId}", job.JobId);
            }
            catch (OperationCanceledException ocex)
            {
                // Server shutting down or request aborted
                _logger.LogWarning(ocex, "Job processing canceled. jobId={JobId}", job.JobId);
                _jobstore.SetFailed(job.JobId, "Canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job processing failed. jobId={JobId}", job.JobId);
                _jobstore.SetFailed(job.JobId, ex.Message);
            }
            finally
            {
                // Read back final state for audit
                var final = _jobstore.Get(job.JobId);
                if (final is null)
                {
                    _logger.LogInformation("Job record no longer present at task end (consumed/removed). jobId={JobId}", job.JobId);
                }
                else
                {
                    _logger.LogInformation("Job task finished. jobId={JobId} finalStatus={Status}", job.JobId, final.Status);
                }
            }
        });

        // Compose polling location
        var location = Url.ActionLink(
            action: nameof(JobsController.GetJob),
            controller: "Jobs",
            values: new { id = job.JobId }) ?? $"/jobs/{job.JobId}";

        Response.Headers.RetryAfter = DefaultRetryAfter.TotalSeconds.ToString("F0");
        _logger.LogInformation("SubmitJob accepted. jobId={JobId} location={Location} retryAfterSeconds={RetryAfter} caller={Caller}", job.JobId, location, DefaultRetryAfter.TotalSeconds.ToString("F0"), callerInfo);

        return Accepted(location, new { jobId = job.JobId });
    }
}
