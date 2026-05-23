using FluentAssertions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Providers.IpStack;
using GeolocationService.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace GeolocationService.Tests.Providers;

public class IpStackProviderTests
{
    private const string SuccessJson = """
    {
      "country_name": "United States",
      "region_name":  "California",
      "city":         "Los Angeles",
      "zip":          "90006",
      "latitude":     34.0476,
      "longitude":    -118.2923,
      "time_zone":  { "id": "America/Los_Angeles" },
      "connection": { "isp": "Los Angeles Dept of Water & Power" },
      "currency":   { "code": "USD" }
    }
    """;

    private const string InvalidIpErrorJson = """
    { "success": false, "error": { "code": 106, "type": "invalid_address", "info": "bad ip" } }
    """;

    private const string GenericErrorJson = """
    { "success": false, "error": { "code": 101, "type": "invalid_access_key", "info": "bad key" } }
    """;

    private static IpStackProvider BuildProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            Options.Create(new IpStackOptions { ApiKey = "test", BaseUrl = "https://test/", Enabled = true }));

    [Fact]
    public async Task Maps_a_successful_response_into_GeolocationResult()
    {
        var provider = BuildProvider(new StubHttpMessageHandler(StubHttpMessageHandler.Json(SuccessJson)));

        var result = await provider.GetLocationAsync("8.8.8.8", CancellationToken.None);

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates!.Lat.Should().BeApproximately(34.0476, 0.001);
        result.TimeZone.Should().Be("America/Los_Angeles");
        result.Isp.Should().Be("Los Angeles Dept of Water & Power");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Throws_InvalidIpException_when_provider_returns_invalid_address_error()
    {
        var provider = BuildProvider(new StubHttpMessageHandler(StubHttpMessageHandler.Json(InvalidIpErrorJson)));

        await Assert.ThrowsAsync<InvalidIpException>(() => provider.GetLocationAsync("0.0.0.0", CancellationToken.None));
    }

    [Fact]
    public async Task Throws_ProviderTerminalException_on_other_errors()
    {
        var provider = BuildProvider(new StubHttpMessageHandler(StubHttpMessageHandler.Json(GenericErrorJson)));

        await Assert.ThrowsAsync<ProviderTerminalException>(() => provider.GetLocationAsync("8.8.8.8", CancellationToken.None));
    }
}
