using GeolocationService.Api.Middleware;
using GeolocationService.Api.Services;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GeolocationService.Api.Endpoints;

public static class GeolocationEndpoint
{
    public static IEndpointRouteBuilder MapGeolocationEndpoint(this IEndpointRouteBuilder app)
    {
        // Explicit IP — preferred when the caller knows the target IP (and required for local testing,
        // because a request originating from the same machine resolves to a loopback address).
        app.MapGet("/api/geolocation/{ip}", ResolveExplicitAsync)
           .WithName("GetGeolocationForIp")
           .WithTags("Geolocation");

        // Auto-resolved — uses the caller's IP from the request (X-Forwarded-For honored via UseForwardedHeaders).
        app.MapGet("/api/geolocation", ResolveAutoAsync)
           .WithName("GetGeolocationForCaller")
           .WithTags("Geolocation");

        return app;
    }

    private static Task<IResult> ResolveExplicitAsync(
        string ip,
        HttpContext httpContext,
        IGeolocationOrchestrator orchestrator,
        CancellationToken ct) =>
        HandleAsync(ip, httpContext, orchestrator, ct);

    private static Task<IResult> ResolveAutoAsync(
        HttpContext httpContext,
        ICallerIpResolver callerIpResolver,
        IGeolocationOrchestrator orchestrator,
        CancellationToken ct) =>
        HandleAsync(callerIpResolver.Resolve(httpContext), httpContext, orchestrator, ct);

    private static async Task<IResult> HandleAsync(
        string? ip,
        HttpContext httpContext,
        IGeolocationOrchestrator orchestrator,
        CancellationToken ct)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.HeaderName] as string;

        if (!IpValidation.IsPublic(ip))
        {
            return Results.BadRequest(new ErrorResponse(
                "InvalidIpAddress",
                "The IP address is not a valid public IPv4/IPv6 address. " +
                "When calling locally without an explicit IP, the resolved caller address is the loopback (127.0.0.1 / ::1), " +
                "which no provider can geolocate — pass an explicit IP via /api/geolocation/{ip} instead.",
                CorrelationId: correlationId));
        }

        var outcome = await orchestrator.ResolveAsync(ip!, ct).ConfigureAwait(false);

        return outcome switch
        {
            GeolocationOutcome.Success s => Results.Ok(s.Result),

            GeolocationOutcome.InvalidIp i => Results.BadRequest(new ErrorResponse(
                "InvalidIpAddress",
                i.Reason,
                CorrelationId: correlationId)),

            GeolocationOutcome.AllFailed f => Results.Json(
                new ErrorResponse(
                    "AllProvidersFailed",
                    "Unable to resolve geolocation. Please try again later.",
                    AttemptedProviders: f.AttemptedProviders,
                    CorrelationId: correlationId),
                statusCode: StatusCodes.Status502BadGateway),

            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
