using System.Net;
using FluentAssertions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Providers.Ip2Location;
using GeolocationService.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace GeolocationService.Tests.Providers;

public class Ip2LocationProviderTests
{
    private const string SuccessJson = """
    {
      "country_name": "United States",
      "region_name":  "California",
      "city_name":    "Los Angeles",
      "zip_code":     "90006",
      "latitude":     34.0476,
      "longitude":    -118.2923,
      "time_zone":    "-07:00",
      "isp":          "Los Angeles Dept of Water & Power",
      "currency":     { "code": "USD" }
    }
    """;

    private const string InvalidIpJson = """
    { "error": { "error_code": 10001, "error_message": "Invalid IP" } }
    """;

    private const string OtherErrorJson = """
    { "error": { "error_code": 20000, "error_message": "Quota exceeded" } }
    """;

    private static Ip2LocationProvider BuildProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            Options.Create(new Ip2LocationOptions { ApiKey = "test", BaseUrl = "https://test/", Enabled = true }));

    [Fact]
    public async Task Maps_a_successful_response_into_GeolocationResult()
    {
        var provider = BuildProvider(new StubHttpMessageHandler(StubHttpMessageHandler.Json(SuccessJson)));

        var result = await provider.GetLocationAsync("8.8.8.8", CancellationToken.None);

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates!.Lng.Should().BeApproximately(-118.2923, 0.001);
        result.TimeZone.Should().Be("-07:00");
        result.Isp.Should().Be("Los Angeles Dept of Water & Power");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Throws_InvalidIpException_when_provider_returns_invalid_ip_error_code()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(InvalidIpJson, HttpStatusCode.BadRequest));
        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<InvalidIpException>(() => provider.GetLocationAsync("0.0.0.0", CancellationToken.None));
    }

    [Fact]
    public async Task Throws_ProviderTerminalException_on_other_errors()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(OtherErrorJson));
        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<ProviderTerminalException>(() => provider.GetLocationAsync("8.8.8.8", CancellationToken.None));
    }
}
