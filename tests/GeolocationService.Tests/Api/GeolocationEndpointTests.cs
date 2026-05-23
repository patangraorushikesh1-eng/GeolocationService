using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using GeolocationService.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeolocationService.Tests.Api;

public class GeolocationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GeolocationEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(params IGeolocationProvider[] providers) =>
        _factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Providers:Geoapify:Enabled"]    = "false",
                    ["Providers:IPStack:Enabled"]     = "false",
                    ["Providers:IP2Location:Enabled"] = "false"
                });
            });
            b.ConfigureTestServices(s =>
            {
                var existing = s.Where(d => d.ServiceType == typeof(IGeolocationProvider)).ToList();
                foreach (var d in existing) s.Remove(d);
                foreach (var p in providers) s.AddSingleton(p);
            });
        }).CreateClient();

    [Theory]
    [InlineData("/api/geolocation/not-an-ip")]
    [InlineData("/api/geolocation/127.0.0.1")]
    [InlineData("/api/geolocation/10.0.0.1")]
    [InlineData("/api/geolocation/192.168.1.1")]
    public async Task Explicit_route_returns_400_when_ip_is_not_a_public_address(string path)
    {
        var fakeNeverCalled = FakeGeolocationProvider.Returning("F", new GeolocationResult());
        var client = CreateClient(fakeNeverCalled);

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fakeNeverCalled.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Auto_route_returns_400_when_caller_ip_is_not_resolvable_or_not_public()
    {
        // TestServer leaves Connection.RemoteIpAddress null → not public → 400.
        var fakeNeverCalled = FakeGeolocationProvider.Returning("F", new GeolocationResult());
        var client = CreateClient(fakeNeverCalled);

        var response = await client.GetAsync("/api/geolocation");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fakeNeverCalled.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Explicit_route_returns_200_with_mapped_payload_when_a_fake_provider_succeeds()
    {
        var fake = FakeGeolocationProvider.Returning(
            "Fake",
            new GeolocationResult
            {
                Country = "Wonderland",
                City = "Alice",
                Coordinates = new Coordinates(1.23, 4.56),
                Currency = "WND"
            });

        var client = CreateClient(fake);
        var response = await client.GetAsync("/api/geolocation/8.8.8.8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GeolocationResult>();
        result!.Country.Should().Be("Wonderland");
        result.City.Should().Be("Alice");
        result.Currency.Should().Be("WND");
    }

    [Fact]
    public async Task Returns_502_with_attempted_providers_when_all_fail()
    {
        var p1 = FakeGeolocationProvider.Throwing("Alpha", () => new ProviderTransientException("Alpha", "x"));
        var p2 = FakeGeolocationProvider.Throwing("Beta",  () => new ProviderTerminalException("Beta",  "y"));

        var client = CreateClient(p1, p2);
        var response = await client.GetAsync("/api/geolocation/8.8.8.8");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Alpha").And.Contain("Beta").And.Contain("AllProvidersFailed");
        body.Should().NotContain("ApiKey", because: "API keys must never leak to the caller");
    }

    [Fact]
    public async Task Health_endpoint_returns_200()
    {
        var fake = FakeGeolocationProvider.Returning("F", new GeolocationResult());
        var client = CreateClient(fake);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
