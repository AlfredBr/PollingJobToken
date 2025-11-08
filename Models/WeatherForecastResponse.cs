namespace api.Models;

public class WeatherForecastResponse
{
    public required string City { get; init; }
    public required DateOnly Date { get; init; }
    public required int TemperatureC { get; init; }
    public required string Summary { get; init; }
}
