using Microsoft.AspNetCore.Http;

namespace GeolocationService.Api.Services;

public sealed class CallerIpResolver : ICallerIpResolver
{
    public string? Resolve(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }
}
