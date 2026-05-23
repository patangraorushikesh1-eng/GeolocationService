using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace GeolocationService.Core.Resilience;

public static class PollyPolicies
{
    public const string ProviderNameKey = "ProviderName";

    public static IAsyncPolicy<HttpResponseMessage> RetryOnceOnTransient(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PollyRetry");

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 1,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(200),
                onRetry: (outcome, delay, attempt, context) =>
                {
                    var providerName = context.TryGetValue(ProviderNameKey, out var n) ? n?.ToString() : "unknown";
                    var status = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.GetType().Name ?? "error";
                    logger.LogWarning(
                        "Retrying provider {Provider} after {DelayMs}ms (attempt {Attempt}, cause: {Cause})",
                        providerName, delay.TotalMilliseconds, attempt, status);
                });
    }
}
