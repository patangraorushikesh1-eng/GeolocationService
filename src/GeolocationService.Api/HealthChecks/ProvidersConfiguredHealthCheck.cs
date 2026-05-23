using GeolocationService.Core.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GeolocationService.Api.HealthChecks;

public sealed class ProvidersConfiguredHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IGeolocationProvider> _providers;

    public ProvidersConfiguredHealthCheck(IEnumerable<IGeolocationProvider> providers)
    {
        _providers = providers;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabled = _providers.Where(p => p.IsEnabled).Select(p => p.Name).ToArray();

        return Task.FromResult(enabled.Length > 0
            ? HealthCheckResult.Healthy(
                "At least one provider is enabled.",
                new Dictionary<string, object> { ["enabled_providers"] = enabled })
            : HealthCheckResult.Unhealthy("No geolocation providers are enabled."));
    }
}
