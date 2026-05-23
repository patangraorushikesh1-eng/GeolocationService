using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Models;

namespace GeolocationService.Tests.Fakes;

public sealed class FakeGeolocationProvider : IGeolocationProvider
{
    private readonly Func<string, Task<GeolocationResult>> _behavior;
    private int _callCount;

    public FakeGeolocationProvider(string name, Func<string, Task<GeolocationResult>> behavior, bool isEnabled = true)
    {
        Name = name;
        IsEnabled = isEnabled;
        _behavior = behavior;
    }

    public string Name { get; }
    public bool IsEnabled { get; }
    public int CallCount => _callCount;

    public async Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct)
    {
        Interlocked.Increment(ref _callCount);
        return await _behavior(ip).ConfigureAwait(false);
    }

    public static FakeGeolocationProvider Returning(string name, GeolocationResult result, bool enabled = true) =>
        new(name, _ => Task.FromResult(result), enabled);

    public static FakeGeolocationProvider Throwing(string name, Func<Exception> exFactory, bool enabled = true) =>
        new(name, _ => Task.FromException<GeolocationResult>(exFactory()), enabled);
}
