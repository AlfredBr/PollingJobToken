using PollingJobToken.Services;

namespace PollingJobToken.AppModels;

public class WeatherForecastRequest : JobRequestBase
{
    public string City { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
}
