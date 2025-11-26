namespace PollingJobToken.Services;

public class JobRequestBase
{
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; } = new object();
}
