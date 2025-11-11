using PollingJobToken.AppModels;
using PollingJobToken.Controllers;
using PollingJobToken.Services;

using Microsoft.AspNetCore.Mvc;

namespace PollingJobToken.AppControllers;

[ApiController]
[Route("jobs/weather")]
public class WeatherForecastController
    : JobSubmissionControllerBase<WeatherForecastRequest, WeatherForecastResponse>
{
    public WeatherForecastController(
        IJobStore store,
        IJobProcessor<WeatherForecastRequest, WeatherForecastResponse> processor,
        ILogger<WeatherForecastController> logger
    )
        : base(store, processor, logger) { }

    [HttpPost]
    public ActionResult Submit([FromBody] WeatherForecastRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.City))
        {
            return BadRequest(new { error = "City is required" });
        }
        return SubmitJobInternal(request);
    }
}
