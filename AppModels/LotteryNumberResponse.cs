namespace PollingJobToken.AppModels;

public class LotteryNumberResponse
{
    public required DateOnly Date { get; init; }

    public required int[] Numbers { get; init; }
}
