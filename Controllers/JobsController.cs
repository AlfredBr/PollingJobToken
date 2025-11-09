using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly IJobStore _jobstore;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobStore store, ILogger<JobsController> logger)
    {
        _jobstore = store;
        _logger = logger;
    }

    // GET /jobs/{id}
    [HttpGet("{id}")]
    public IActionResult GetJob(string id)
    {
        _logger.LogInformation("GetJob request received. jobId={JobId}", id);

        var job = _jobstore.Get(id);
        if (job is null)
        {
            // Distinguish expired vs never existed
            if (_jobstore.WasRecentlyExpired(id))
            {
                _logger.LogWarning("GetJob request for expired job. jobId={JobId}", id);
                return Problem(title: "Job expired", statusCode: StatusCodes.Status410Gone);
            }
            _logger.LogWarning("GetJob not found. jobId={JobId}", id);
            return NotFound();
        }

        switch (job.Status)
        {
            case JobStatus.Completed:
                _logger.LogInformation("GetJob completed. jobId={JobId} completedAt={CompletedAt}", id, job.CompletedAt);
                return Ok(job);

            case JobStatus.Failed:
                _logger.LogError("GetJob failed. jobId={JobId} message={Message}", id, job.Message);
                return Problem(
                    title: "Job failed",
                    detail: job.Message,
                    statusCode: StatusCodes.Status500InternalServerError);

            case JobStatus.Canceled:
                _logger.LogWarning("GetJob canceled. jobId={JobId}", id);
                return Problem(
                    title: "Job canceled",
                    statusCode: StatusCodes.Status410Gone);

            default:
                _logger.LogInformation("GetJob in progress. jobId={JobId} status={Status}", id, job.Status);
                return new ObjectResult(new { status = job.Status.ToString(), jobId = job.JobId })
                {
                    StatusCode = StatusCodes.Status202Accepted
                };
        }
    }

    // DELETE /jobs/{id}
    [HttpDelete("{id}")]
    public IActionResult CancelJob(string id, bool purge = false)
    {
        _logger.LogInformation("CancelJob request received. jobId={JobId}, purge={Purge}", id, purge);

        if (purge)
        {
            var existed = _jobstore.Get(id) is not null;
            if (existed)
            {
                _jobstore.PurgeJob(id);
                _logger.LogInformation("PurgeJob succeeded. jobId={JobId}", id);
                return NoContent();
            }

            if (_jobstore.WasRecentlyExpired(id))
            {
                _logger.LogWarning("PurgeJob failed; job already expired. jobId={JobId}", id);
                return Problem(title: "Job expired", statusCode: StatusCodes.Status410Gone);
            }

            _logger.LogWarning("PurgeJob not found. jobId={JobId}", id);
            return NotFound();
        }

        var ok = _jobstore.TryCancel(id);
        if (ok)
        {
            _logger.LogInformation("CancelJob succeeded. jobId={JobId}", id);
            return NoContent();
        }

        // For logging clarity, check if it existed but is not cancelable or recently expired
        var existing = _jobstore.Get(id);
        if (existing is not null)
        {
            _logger.LogWarning("CancelJob not allowed; job is in terminal state. jobId={JobId} status={Status}", id, existing.Status);
        }
        else if (_jobstore.WasRecentlyExpired(id))
        {
            _logger.LogWarning("CancelJob failed; job expired. jobId={JobId}", id);
        }
        else
        {
            _logger.LogWarning("CancelJob not found. jobId={JobId}", id);
        }

        return NotFound();
    }    
}
