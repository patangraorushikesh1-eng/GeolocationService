using System.Text.Json.Serialization;

namespace GeolocationService.Api.Endpoints;

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")]               string Error,
    [property: JsonPropertyName("message")]             string Message,
    [property: JsonPropertyName("attempted_providers")] IReadOnlyList<string>? AttemptedProviders = null,
    [property: JsonPropertyName("correlation_id")]      string? CorrelationId = null);
