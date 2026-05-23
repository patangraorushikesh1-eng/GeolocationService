using System.Text.Json.Serialization;

namespace GeolocationService.Core.Providers.Geoapify;

internal sealed class GeoapifyResponse
{
    [JsonPropertyName("country")]    public NamedWithCurrency? Country { get; set; }
    [JsonPropertyName("state")]      public Named? State { get; set; }
    [JsonPropertyName("city")]       public Named? City { get; set; }
    [JsonPropertyName("postcode")]   public string? Postcode { get; set; }
    [JsonPropertyName("location")]   public LocationBlock? Location { get; set; }
    [JsonPropertyName("timezone")]   public Named? Timezone { get; set; }
    [JsonPropertyName("statusCode")] public int? StatusCode { get; set; }
    [JsonPropertyName("message")]    public string? Message { get; set; }

    internal sealed class Named
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    internal sealed class NamedWithCurrency
    {
        [JsonPropertyName("name")]          public string? Name { get; set; }
        [JsonPropertyName("currency_code")] public string? CurrencyCode { get; set; }
    }

    internal sealed class LocationBlock
    {
        [JsonPropertyName("latitude")]  public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
    }
}
