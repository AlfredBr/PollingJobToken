using PollingJobToken.Services;

namespace PollingJobToken.AppModels;

public class WeatherForecastRequest : IJobRequest
{
    public string City { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public string Message { get; set; } = string.Empty;
}
