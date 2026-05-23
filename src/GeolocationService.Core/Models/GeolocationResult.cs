using System.Text.Json.Serialization;

namespace GeolocationService.Core.Models;

public sealed record GeolocationResult
{
    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("zipcode")]
    public string? Zipcode { get; init; }

    [JsonPropertyName("coordinates")]
    public Coordinates? Coordinates { get; init; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; init; }

    [JsonPropertyName("isp")]
    public string? Isp { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}
