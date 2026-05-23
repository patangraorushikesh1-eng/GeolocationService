using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using GeolocationService.Core.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace GeolocationService.Core.Providers.IpStack;

public sealed class IpStackProvider : IGeolocationProvider
{
    // IPStack error codes that indicate the IP itself is invalid (not a transient/config issue).
    private static readonly HashSet<int> InvalidIpErrorCodes = new() { 106 };

    private readonly HttpClient _httpClient;
    private readonly IpStackOptions _options;

    public IpStackProvider(HttpClient httpClient, IOptions<IpStackOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string Name => IpStackOptions.ProviderName;
    public bool IsEnabled => _options.Enabled;

    public async Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct)
    {
        var requestUri = $"{Uri.EscapeDataString(ip)}?access_key={Uri.EscapeDataString(_options.ApiKey)}";
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
            throw new ProviderTransientException(Name, "Network error contacting IPStack.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ProviderTransientException(Name, "Timeout contacting IPStack.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderTerminalException(Name, $"IPStack returned HTTP {(int)response.StatusCode}.");
            }

            IpStackResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<IpStackResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new ProviderTerminalException(Name, "Failed to parse IPStack response.", ex);
            }

            if (payload is null)
            {
                throw new ProviderTerminalException(Name, "Empty response body from IPStack.");
            }

            if (payload.Error is not null || payload.Success == false)
            {
                var code = payload.Error?.Code ?? 0;
                var type = payload.Error?.Type ?? "unknown";
                if (InvalidIpErrorCodes.Contains(code))
                {
                    throw new InvalidIpException("IPStack rejected the IP address as invalid.");
                }
                throw new ProviderTerminalException(Name, $"IPStack error: {type} (code {code}).");
            }

            return Map(payload);
        }
    }

    private static GeolocationResult Map(IpStackResponse r) => new()
    {
        Country  = r.CountryName,
        State    = r.RegionName,
        City     = r.City,
        Zipcode  = r.Zip,
        Coordinates = r.Latitude.HasValue && r.Longitude.HasValue
            ? new Coordinates(r.Latitude.Value, r.Longitude.Value)
            : null,
        TimeZone = r.TimeZone?.Id,
        Isp      = r.Connection?.Isp,
        Currency = r.Currency?.Code
    };
}
