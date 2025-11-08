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
        var job = _jobstore.Get(id);
        if (job is null)
        {
			// Distinguish expired vs never existed
			if (_jobstore.WasRecentlyExpired(id))
			{
				return Problem(title: "Job expired", statusCode: StatusCodes.Status410Gone);
			}
            return NotFound();
        }

        return job.Status switch
        {
            JobStatus.Completed => Ok(job),
            JobStatus.Failed => Problem(
                title: "Job failed",
                detail: job.Message,
                statusCode: StatusCodes.Status500InternalServerError),
            JobStatus.Canceled => Problem(
                title: "Job canceled", 
                statusCode: StatusCodes.Status410Gone),
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
}
