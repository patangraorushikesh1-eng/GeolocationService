using System.Text.Json.Serialization;

namespace GeolocationService.Core.Models;

public sealed record Coordinates(
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lng")] double Lng);
