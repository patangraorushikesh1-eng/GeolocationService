using System.ComponentModel.DataAnnotations;

namespace GeolocationService.Core.Providers;

public abstract class ProviderOptionsBase
{
    public bool Enabled { get; set; } = true;

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 5;
}
