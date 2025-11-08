using Microsoft.AspNetCore.Mvc;
using api.Models;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("jobs")]
public partial class JobsController : ControllerBase
{
    private readonly IJobStore _jobstore;
    private readonly ILogger<JobsController> _logger;
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(2);

    public JobsController(IJobStore store, ILogger<JobsController> logger)
    {
        _jobstore = store;
        _logger = logger;
    }

    // POST /jobs ->202 Accepted with token (jobId) and Location to poll
    [HttpPost]
    public ActionResult<JobResult> SubmitJob()
    {
        var job = _jobstore.Create();

        // Kick off work asynchronously (partial method to implement later)
        // The partial method can be implemented in another file to process the job
        _ = Task.Run(() => ProcessJobAsync(job.JobId));

        var location = Url.ActionLink(nameof(GetJob), values: new { id = job.JobId }) ?? $"/jobs/{job.JobId}";
        return Accepted(location, new { jobId = job.JobId });
    }

    // GET /jobs/{id}
    [HttpGet("{id}")]
    public IActionResult GetJob(string jobId)
    {
        var job = _jobstore.Get(jobId);
        if (job is null) return NotFound();

        return job.Status switch
        {
            JobStatus.Completed => Ok(job),
            JobStatus.Failed => Problem(title: "Job failed", detail: job.Message, statusCode: StatusCodes.Status500InternalServerError),
            JobStatus.Canceled => Problem(title: "Job canceled", statusCode: StatusCodes.Status410Gone),
            _ => new ObjectResult(new { status = job.Status.ToString(), jobId = job.JobId })
            {
                StatusCode = StatusCodes.Status202Accepted
            }
        };
    }

    // DELETE /jobs/{id}
    [HttpDelete("{id}")]
    public IActionResult CancelJob(string id)
    {
        var ok = _jobstore.TryCancel(id);
        return ok ? NoContent() : NotFound();
    }

    // Partial method for background job work.
    private partial Task ProcessJobAsync(string jobId);
}
