using GeolocationService.Core.Models;

namespace GeolocationService.Core.Abstractions;

public interface IGeolocationOrchestrator
{
    Task<GeolocationOutcome> ResolveAsync(string ip, CancellationToken ct);
}
