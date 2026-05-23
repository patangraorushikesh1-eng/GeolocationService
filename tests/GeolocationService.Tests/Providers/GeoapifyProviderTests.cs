using System.Net;
using FluentAssertions;
using GeolocationService.Core.Providers.Geoapify;
using GeolocationService.Core.Resilience;
using GeolocationService.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GeolocationService.Tests.Providers;

public class GeoapifyProviderTests
{
    private const string SampleJson = """
    {
      "country":  { "name": "United States", "currency_code": "USD" },
      "state":    { "name": "California" },
      "city":     { "name": "Los Angeles" },
      "postcode": "90006",
      "location": { "latitude": 34.0476, "longitude": -118.2923 },
      "timezone": { "name": "America/Los_Angeles" }
    }
    """;

    private static GeoapifyProvider BuildProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            Options.Create(new GeoapifyOptions { ApiKey = "test", BaseUrl = "https://test/", Enabled = true }));

    [Fact]
    public async Task Maps_a_successful_response_into_GeolocationResult()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(SampleJson));
        var provider = BuildProvider(handler);

        var result = await provider.GetLocationAsync("8.8.8.8", CancellationToken.None);

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates!.Lat.Should().BeApproximately(34.0476, 0.001);
        result.Coordinates!.Lng.Should().BeApproximately(-118.2923, 0.001);
        result.TimeZone.Should().Be("America/Los_Angeles");
        result.Currency.Should().Be("USD");
        result.Isp.Should().BeNull();
    }

    [Fact]
    public async Task Polly_retries_once_on_a_transient_5xx_then_succeeds()
    {
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            StubHttpMessageHandler.Json(SampleJson));

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new GeoapifyOptions { ApiKey = "test", BaseUrl = "https://test/", Enabled = true }));
        services.AddSingleton(NullLoggerFactory.Instance);
        services
            .AddHttpClient<GeoapifyProvider>((sp, c) =>
            {
                c.BaseAddress = new Uri("https://test/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddPolicyHandler((sp, _) => PollyPolicies.RetryOnceOnTransient(NullLoggerFactory.Instance));

        using var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<GeoapifyProvider>();

        var result = await provider.GetLocationAsync("8.8.8.8", CancellationToken.None);

        result.City.Should().Be("Los Angeles");
        handler.CallCount.Should().Be(2);
    }
}
