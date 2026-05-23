using GeolocationService.Core.Abstractions;

namespace GeolocationService.Core.Orchestration;

public sealed class RoundRobinProviderSelector : IProviderSelector
{
    private readonly IReadOnlyList<IGeolocationProvider> _providers;
    private long _counter = -1;

    public RoundRobinProviderSelector(IEnumerable<IGeolocationProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public IReadOnlyList<IGeolocationProvider> GetOrderedProvidersForRequest()
    {
        var enabled = _providers.Where(p => p.IsEnabled).ToArray();
        if (enabled.Length == 0)
        {
            return Array.Empty<IGeolocationProvider>();
        }

        var next = Interlocked.Increment(ref _counter);
        var start = (int)(((next % enabled.Length) + enabled.Length) % enabled.Length);

        var ordered = new IGeolocationProvider[enabled.Length];
        for (var i = 0; i < enabled.Length; i++)
        {
            ordered[i] = enabled[(start + i) % enabled.Length];
        }
        return ordered;
    }
}
