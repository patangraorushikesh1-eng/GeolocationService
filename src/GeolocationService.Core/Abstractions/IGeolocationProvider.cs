using GeolocationService.Core.Models;

namespace GeolocationService.Core.Abstractions;

public interface IGeolocationProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct);
}
