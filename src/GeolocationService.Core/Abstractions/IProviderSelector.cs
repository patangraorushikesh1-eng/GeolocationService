namespace GeolocationService.Core.Abstractions;

public interface IProviderSelector
{
    IReadOnlyList<IGeolocationProvider> GetOrderedProvidersForRequest();
}
