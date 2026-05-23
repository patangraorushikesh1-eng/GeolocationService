using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using GeolocationService.Core.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace GeolocationService.Core.Providers.Geoapify;

public sealed class GeoapifyProvider : IGeolocationProvider
{
    private readonly HttpClient _httpClient;
    private readonly GeoapifyOptions _options;

    public GeoapifyProvider(HttpClient httpClient, IOptions<GeoapifyOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => GeoapifyOptions.ProviderName;
    public bool IsEnabled => _options.Enabled;

    public async Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct)
    {
        var requestUri = $"?ip={Uri.EscapeDataString(ip)}&apiKey={Uri.EscapeDataString(_options.ApiKey)}";
        var pollyContext = new Context { [PollyPolicies.ProviderNameKey] = Name };

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.SetPolicyExecutionContext(pollyContext);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderTransientException(Name, "Network error contacting Geoapify.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ProviderTransientException(Name, "Timeout contacting Geoapify.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new InvalidIpException("Geoapify rejected the IP address as invalid.");
                }
                throw new ProviderTerminalException(Name, $"Geoapify returned HTTP {(int)response.StatusCode}.");
            }

            GeoapifyResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<GeoapifyResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new ProviderTerminalException(Name, "Failed to parse Geoapify response.", ex);
            }

            if (payload is null)
            {
                throw new ProviderTerminalException(Name, "Empty response body from Geoapify.");
            }

            if (payload.StatusCode is >= 400 || !string.IsNullOrEmpty(payload.Message))
            {
                throw new ProviderTerminalException(Name, $"Geoapify error: {payload.Message ?? "unknown"}.");
            }

            return Map(payload);
        }
    }

    private static GeolocationResult Map(GeoapifyResponse r) => new()
    {
        Country  = r.Country?.Name,
        State    = r.State?.Name,
        City     = r.City?.Name,
        Zipcode  = r.Postcode,
        Coordinates = r.Location is null ? null : new Coordinates(r.Location.Latitude, r.Location.Longitude),
        TimeZone = r.Timezone?.Name,
        Isp      = null,
        Currency = r.Country?.CurrencyCode
    };
}
