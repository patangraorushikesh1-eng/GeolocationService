using FluentAssertions;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Exceptions;
using GeolocationService.Core.Models;
using GeolocationService.Core.Orchestration;
using GeolocationService.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GeolocationService.Tests.Orchestration;

public class GeolocationOrchestratorTests
{
    private static readonly GeolocationResult SampleResult = new() { Country = "United States", City = "LA" };

    private static GeolocationOrchestrator BuildOrchestrator(
        IEnumerable<IGeolocationProvider> providers,
        ILogger<GeolocationOrchestrator>? logger = null)
    {
        var selector = new RoundRobinProviderSelector(providers);
        return new GeolocationOrchestrator(selector, logger ?? NullLogger<GeolocationOrchestrator>.Instance);
    }

    [Fact]
    public async Task Returns_success_when_first_provider_responds()
    {
        var primary = FakeGeolocationProvider.Returning("Primary", SampleResult);
        var secondary = FakeGeolocationProvider.Throwing("Secondary", () => new ProviderTransientException("Secondary", "should not be called"));

        var orchestrator = BuildOrchestrator(new[] { primary, secondary });

        var outcome = await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.Success>()
            .Which.ProviderUsed.Should().Be("Primary");
        primary.CallCount.Should().Be(1);
        secondary.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Falls_back_to_next_provider_when_first_fails_transiently()
    {
        var failing = FakeGeolocationProvider.Throwing("First", () => new ProviderTransientException("First", "boom"));
        var succeeding = FakeGeolocationProvider.Returning("Second", SampleResult);

        var orchestrator = BuildOrchestrator(new[] { failing, succeeding });

        var outcome = await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.Success>()
            .Which.ProviderUsed.Should().Be("Second");
        failing.CallCount.Should().Be(1);
        succeeding.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Falls_back_through_terminal_failures_as_well()
    {
        var terminal = FakeGeolocationProvider.Throwing("First", () => new ProviderTerminalException("First", "bad"));
        var succeeding = FakeGeolocationProvider.Returning("Second", SampleResult);

        var orchestrator = BuildOrchestrator(new[] { terminal, succeeding });

        var outcome = await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.Success>()
            .Which.ProviderUsed.Should().Be("Second");
    }

    [Fact]
    public async Task Returns_AllFailed_with_full_attempted_list_when_every_provider_fails()
    {
        var p1 = FakeGeolocationProvider.Throwing("A", () => new ProviderTransientException("A", "x"));
        var p2 = FakeGeolocationProvider.Throwing("B", () => new ProviderTerminalException("B", "y"));
        var p3 = FakeGeolocationProvider.Throwing("C", () => new ProviderTransientException("C", "z"));

        var orchestrator = BuildOrchestrator(new[] { p1, p2, p3 });

        var outcome = await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.AllFailed>()
            .Which.AttemptedProviders.Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }

    [Fact]
    public async Task Short_circuits_with_InvalidIp_and_does_not_try_other_providers()
    {
        var first = FakeGeolocationProvider.Throwing("First", () => new InvalidIpException("invalid"));
        var second = FakeGeolocationProvider.Returning("Second", SampleResult);

        var orchestrator = BuildOrchestrator(new[] { first, second });

        var outcome = await orchestrator.ResolveAsync("0.0.0.0", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.InvalidIp>();
        second.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Logs_a_Critical_alert_when_all_providers_fail()
    {
        var logger = new Mock<ILogger<GeolocationOrchestrator>>();

        var p1 = FakeGeolocationProvider.Throwing("A", () => new ProviderTransientException("A", "x"));
        var p2 = FakeGeolocationProvider.Throwing("B", () => new ProviderTerminalException("B", "y"));
        var orchestrator = BuildOrchestrator(new[] { p1, p2 }, logger.Object);

        await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("All geolocation providers failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task A_new_fake_provider_participates_without_orchestrator_changes()
    {
        var failingReal1 = FakeGeolocationProvider.Throwing("RealA", () => new ProviderTransientException("RealA", "fail"));
        var failingReal2 = FakeGeolocationProvider.Throwing("RealB", () => new ProviderTransientException("RealB", "fail"));
        var brandNewProvider = FakeGeolocationProvider.Returning("BrandNew", SampleResult);

        var orchestrator = BuildOrchestrator(new[] { failingReal1, failingReal2, brandNewProvider });

        var outcome = await orchestrator.ResolveAsync("8.8.8.8", CancellationToken.None);

        outcome.Should().BeOfType<GeolocationOutcome.Success>()
            .Which.ProviderUsed.Should().Be("BrandNew");
        brandNewProvider.CallCount.Should().Be(1);
    }
}
