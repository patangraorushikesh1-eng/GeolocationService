using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeolocationService.Core.Orchestration;

public sealed class GeolocationOrchestrator : IGeolocationOrchestrator
{
    private readonly IProviderSelector _selector;
    private readonly ILogger<GeolocationOrchestrator> _logger;

    public GeolocationOrchestrator(IProviderSelector selector, ILogger<GeolocationOrchestrator> logger)
    {
        _selector = selector;
        _logger = logger;
    }

    public async Task<GeolocationOutcome> ResolveAsync(string ip, CancellationToken ct)
    {
        var providers = _selector.GetOrderedProvidersForRequest();
        if (providers.Count == 0)
        {
            _logger.LogCritical("No geolocation providers are enabled for IP {Ip}", ip);
            return new GeolocationOutcome.AllFailed(Array.Empty<string>());
        }

        var attempted = new List<string>(providers.Count);

        foreach (var provider in providers)
        {
            _logger.LogInformation("Calling provider {Provider} for IP {Ip}", provider.Name, ip);
            try
            {
                var result = await provider.GetLocationAsync(ip, ct).ConfigureAwait(false);
                _logger.LogInformation("Provider {Provider} returned a successful result for IP {Ip}", provider.Name, ip);
                return new GeolocationOutcome.Success(result, provider.Name);
            }
            catch (InvalidIpException ex)
            {
                _logger.LogInformation("Provider {Provider} reported invalid IP {Ip}: {Reason}", provider.Name, ip, ex.Message);
                return new GeolocationOutcome.InvalidIp(ex.Message);
            }
            catch (ProviderTransientException ex)
            {
                attempted.Add(provider.Name);
                _logger.LogWarning("Provider {Provider} failed (transient) for IP {Ip}: {Reason}", provider.Name, ip, ex.Message);
            }
            catch (ProviderTerminalException ex)
            {
                attempted.Add(provider.Name);
                _logger.LogWarning("Provider {Provider} failed (terminal) for IP {Ip}: {Reason}", provider.Name, ip, ex.Message);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempted.Add(provider.Name);
                _logger.LogWarning(ex, "Provider {Provider} threw an unexpected error for IP {Ip}", provider.Name, ip);
            }
        }

        _logger.LogCritical(
            "All geolocation providers failed for IP {Ip}. Attempted providers: {@AttemptedProviders}",
            ip, attempted);

        return new GeolocationOutcome.AllFailed(attempted);
    }
}
