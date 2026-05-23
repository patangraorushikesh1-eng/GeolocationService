using System.Text.Json.Serialization;

namespace GeolocationService.Core.Providers.Ip2Location;

internal sealed class Ip2LocationResponse
{
    [JsonPropertyName("error")]         public ErrorBlock? Error { get; set; }

    [JsonPropertyName("country_name")]  public string? CountryName { get; set; }
    [JsonPropertyName("region_name")]   public string? RegionName { get; set; }
    [JsonPropertyName("city_name")]     public string? CityName { get; set; }
    [JsonPropertyName("zip_code")]      public string? ZipCode { get; set; }
    [JsonPropertyName("latitude")]      public double? Latitude { get; set; }
    [JsonPropertyName("longitude")]     public double? Longitude { get; set; }
    [JsonPropertyName("time_zone")]     public string? TimeZone { get; set; }
    [JsonPropertyName("isp")]           public string? Isp { get; set; }
    [JsonPropertyName("currency")]      public CurrencyBlock? Currency { get; set; }

    internal sealed class ErrorBlock
    {
        [JsonPropertyName("error_code")]    public int ErrorCode { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }

    internal sealed class CurrencyBlock
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
    }
}
