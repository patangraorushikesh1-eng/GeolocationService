namespace GeolocationService.Core.Models;

public abstract record GeolocationOutcome
{
    public sealed record Success(GeolocationResult Result, string ProviderUsed) : GeolocationOutcome;

    public sealed record InvalidIp(string Reason) : GeolocationOutcome;

    public sealed record AllFailed(IReadOnlyList<string> AttemptedProviders) : GeolocationOutcome;
}
