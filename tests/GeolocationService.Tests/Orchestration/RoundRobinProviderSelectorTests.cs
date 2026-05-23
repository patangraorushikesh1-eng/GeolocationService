using FluentAssertions;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Models;
using GeolocationService.Core.Orchestration;
using GeolocationService.Tests.Fakes;
using Xunit;

namespace GeolocationService.Tests.Orchestration;

public class RoundRobinProviderSelectorTests
{
    private static IGeolocationProvider Provider(string name, bool enabled = true) =>
        FakeGeolocationProvider.Returning(name, new GeolocationResult(), enabled);

    [Fact]
    public void Each_request_starts_with_the_next_provider_in_order()
    {
        var providers = new[] { Provider("Geoapify"), Provider("IPStack"), Provider("IP2Location") };
        var selector = new RoundRobinProviderSelector(providers);

        var firstStarts = Enumerable.Range(0, 6)
            .Select(_ => selector.GetOrderedProvidersForRequest()[0].Name)
            .ToArray();

        firstStarts.Should().Equal("Geoapify", "IPStack", "IP2Location", "Geoapify", "IPStack", "IP2Location");
    }

    [Fact]
    public void Ordered_list_always_contains_every_enabled_provider_exactly_once()
    {
        var providers = new[] { Provider("A"), Provider("B"), Provider("C") };
        var selector = new RoundRobinProviderSelector(providers);

        for (var i = 0; i < 10; i++)
        {
            var ordered = selector.GetOrderedProvidersForRequest();
            ordered.Select(p => p.Name).Should().BeEquivalentTo(new[] { "A", "B", "C" });
            ordered.Should().HaveCount(3);
        }
    }

    [Fact]
    public void Disabled_providers_are_excluded_from_rotation()
    {
        var providers = new[]
        {
            Provider("Enabled1"),
            Provider("Disabled", enabled: false),
            Provider("Enabled2")
        };
        var selector = new RoundRobinProviderSelector(providers);

        for (var i = 0; i < 5; i++)
        {
            var ordered = selector.GetOrderedProvidersForRequest();
            ordered.Select(p => p.Name).Should().NotContain("Disabled");
            ordered.Should().HaveCount(2);
        }
    }

    [Fact]
    public void Returns_empty_when_no_providers_enabled()
    {
        var providers = new[] { Provider("X", enabled: false) };
        var selector = new RoundRobinProviderSelector(providers);

        selector.GetOrderedProvidersForRequest().Should().BeEmpty();
    }

    [Fact]
    public async Task Round_robin_distribution_is_balanced_under_concurrent_load()
    {
        var providers = new[] { Provider("A"), Provider("B"), Provider("C") };
        var selector = new RoundRobinProviderSelector(providers);
        const int totalRequests = 3000;

        var firstPicks = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, totalRequests),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            (_, _) =>
            {
                firstPicks.Add(selector.GetOrderedProvidersForRequest()[0].Name);
                return ValueTask.CompletedTask;
            });

        var counts = firstPicks.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());

        counts.Should().HaveCount(3);
        var expected = totalRequests / 3;
        foreach (var (_, count) in counts)
        {
            // Interlocked.Increment guarantees each request gets a unique index — distribution should be exact ±1
            count.Should().BeInRange(expected - 1, expected + 1);
        }
    }
}
