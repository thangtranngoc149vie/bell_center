namespace BellCenter.Api.Options;

public sealed class SignalRNegotiationOptions
{
    public string Url { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; } = 3600;
}
