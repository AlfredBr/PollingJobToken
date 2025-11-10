namespace api.AppModels;

public class WeatherForecastRequest
{
    public string City { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
}
