using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using GeolocationService.Core.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace GeolocationService.Core.Providers.Ip2Location;

public sealed class Ip2LocationProvider : IGeolocationProvider
{
    // IP2Location.io error codes that indicate the IP itself is invalid.
    private static readonly HashSet<int> InvalidIpErrorCodes = new() { 10001, 10002 };

    private readonly HttpClient _httpClient;
    private readonly Ip2LocationOptions _options;

    public Ip2LocationProvider(HttpClient httpClient, IOptions<Ip2LocationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => Ip2LocationOptions.ProviderName;
    public bool IsEnabled => _options.Enabled;

    public async Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct)
    {
        var requestUri = $"?key={Uri.EscapeDataString(_options.ApiKey)}&ip={Uri.EscapeDataString(ip)}";
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
            throw new ProviderTransientException(Name, "Network error contacting IP2Location.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ProviderTransientException(Name, "Timeout contacting IP2Location.", ex);
        }

        using (response)
        {
            // IP2Location.io can return 4xx with an error body for invalid IPs.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            {
                throw new ProviderTerminalException(Name, $"IP2Location returned HTTP {(int)response.StatusCode}.");
            }

            Ip2LocationResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<Ip2LocationResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new ProviderTerminalException(Name, "Failed to parse IP2Location response.", ex);
            }

            if (payload is null)
            {
                throw new ProviderTerminalException(Name, "Empty response body from IP2Location.");
            }

            if (payload.Error is not null)
            {
                if (InvalidIpErrorCodes.Contains(payload.Error.ErrorCode))
                {
                    throw new InvalidIpException("IP2Location rejected the IP address as invalid.");
                }
                throw new ProviderTerminalException(
                    Name,
                    $"IP2Location error: {payload.Error.ErrorMessage ?? "unknown"} (code {payload.Error.ErrorCode}).");
            }

            return Map(payload);
        }
    }

    private static GeolocationResult Map(Ip2LocationResponse r) => new()
    {
        Country  = r.CountryName,
        State    = r.RegionName,
        City     = r.CityName,
        Zipcode  = r.ZipCode,
        Coordinates = r.Latitude.HasValue && r.Longitude.HasValue
            ? new Coordinates(r.Latitude.Value, r.Longitude.Value)
            : null,
        TimeZone = r.TimeZone,
        Isp      = r.Isp,
        Currency = r.Currency?.Code
    };
}
