using System.Text.Json.Serialization;

namespace GeolocationService.Core.Providers.IpStack;

internal sealed class IpStackResponse
{
    [JsonPropertyName("success")]      public bool? Success { get; set; }
    [JsonPropertyName("error")]        public ErrorBlock? Error { get; set; }

    [JsonPropertyName("country_name")] public string? CountryName { get; set; }
    [JsonPropertyName("region_name")]  public string? RegionName { get; set; }
    [JsonPropertyName("city")]         public string? City { get; set; }
    [JsonPropertyName("zip")]          public string? Zip { get; set; }
    [JsonPropertyName("latitude")]     public double? Latitude { get; set; }
    [JsonPropertyName("longitude")]    public double? Longitude { get; set; }
    [JsonPropertyName("time_zone")]    public TimeZoneBlock? TimeZone { get; set; }
    [JsonPropertyName("connection")]   public ConnectionBlock? Connection { get; set; }
    [JsonPropertyName("currency")]     public CurrencyBlock? Currency { get; set; }

    internal sealed class ErrorBlock
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("info")] public string? Info { get; set; }
    }

    internal sealed class TimeZoneBlock
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    internal sealed class ConnectionBlock
    {
        [JsonPropertyName("isp")] public string? Isp { get; set; }
    }

    internal sealed class CurrencyBlock
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
    }
}
