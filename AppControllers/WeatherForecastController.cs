using api.AppModels;
using api.Controllers;
using api.Services;

using Microsoft.AspNetCore.Mvc;

namespace api.AppControllers;

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
