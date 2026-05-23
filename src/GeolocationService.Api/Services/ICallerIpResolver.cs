using Microsoft.AspNetCore.Http;

namespace GeolocationService.Api.Services;

public interface ICallerIpResolver
{
    /// <summary>
    /// Returns the caller's IP as seen by the server, or null if it cannot be determined.
    /// When the service is behind a reverse proxy and <c>UseForwardedHeaders</c> is wired,
    /// the X-Forwarded-For value is already reflected in <c>HttpContext.Connection.RemoteIpAddress</c>.
    /// </summary>
    string? Resolve(HttpContext context);
}
