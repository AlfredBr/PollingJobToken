using PollingJobToken.AppModels;
using PollingJobToken.Services;

namespace PollingJobToken.AppServices;

public class LotteryNumberJobProcessor
    : IJobProcessor<LotteryNumberRequest, LotteryNumberResponse>
{
    public Task<LotteryNumberResponse> RunAsync(
        LotteryNumberRequest request,
        CancellationToken cancellationToken
    )
    {
        // Simulate long-running work
        return Task.Run(
            async () =>
            {
                // pretend this takes 15 seconds to produce ...
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                var date = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-3);
                var rng = new Random(date.GetHashCode());
                
                return new LotteryNumberResponse
                {                    
                    Date = date,
                    Numbers = Enumerable.Range(1, 48)
                        .OrderBy(_ => rng.Next())
                        .Take(5)
                        .OrderBy(n => n)
                        .ToArray()
                };
            },
            cancellationToken
        );
    }
}
