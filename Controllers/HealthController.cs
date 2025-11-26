using PollingJobToken.Models;
using PollingJobToken.Services;
using Microsoft.AspNetCore.Mvc;

namespace PollingJobToken.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    // GET /health
    [HttpGet]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Health check request received.");
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow, Hostname = Environment.MachineName });
    }
}
