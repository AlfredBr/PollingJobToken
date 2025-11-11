using PollingJobToken.Services;

namespace PollingJobToken.AppModels;

public class LotteryNumberRequest : IJobRequest
{
	public string Message { get; set; } = string.Empty;
}
