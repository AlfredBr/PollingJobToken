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
                var low = -10;
                var high = 35;
                var temperatureC = rng.Next(low, high);
                var summaries = new[]
                {
                    "Freezing",   // coldest
                    "Bracing",
                    "Chilly",
                    "Cool",
                    "Mild",
                    "Warm",
                    "Balmy",
                    "Hot",
                    "Sweltering",
                    "Scorching"   // hottest
                };

                // Map temperature range [low .. high] to index [0 .. summaries.Length-1]
                // Use linear bucketing with clamping to pick an appropriate word for the generated temperature.
                var span = high - low;
                var idx = Math.Clamp(((temperatureC + Math.Abs(low)) * summaries.Length) / span, 0, summaries.Length - 1);
                var summary = summaries[idx];

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
