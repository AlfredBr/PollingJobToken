using PollingJobToken.AppModels;
using PollingJobToken.Services;

namespace PollingJobToken.AppServices;

public class WeatherForecastJobProcessor
    : IJobProcessor<WeatherForecastRequest, WeatherForecastResponse>
{
    public Task<WeatherForecastResponse> RunAsync(
        WeatherForecastRequest request,
        CancellationToken cancellationToken
    )
    {
        // Simulate long-running work
        return Task.Run(
            async () =>
            {
                // pretend this takes 15 seconds to produce ...
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var rng = new Random(HashCode.Combine(request.City.GetHashCode(), date.GetHashCode()));
                var temperatureC = rng.Next(-10, 36);
                var summaries = new[]
                {
                    "Freezing",
                    "Bracing",
                    "Chilly",
                    "Cool",
                    "Mild",
                    "Warm",
                    "Balmy",
                    "Hot",
                    "Sweltering",
                    "Scorching"
                };
                var summary = summaries[rng.Next(summaries.Length)];
                return new WeatherForecastResponse
                {
                    City = request.City,
                    Date = date,
                    TemperatureC = temperatureC,
                    Summary = $"The weather will be {summary}"
                };
            },
            cancellationToken
        );
    }
}
