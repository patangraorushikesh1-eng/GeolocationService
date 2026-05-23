using System.Net;

namespace GeolocationService.Api.Services;

public static class IpValidation
{
    /// <summary>
    /// True only for syntactically valid, non-loopback, non-private, non-link-local addresses.
    /// </summary>
    public static bool IsPublic(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var addr))
        {
            return false;
        }

        if (IPAddress.IsLoopback(addr))
        {
            return false;
        }

        var bytes = addr.GetAddressBytes();
        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            if (bytes[0] == 10) return false;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            if (bytes[0] == 169 && bytes[1] == 254) return false; // link-local
            if (bytes[0] == 0) return false;
            if (bytes[0] >= 224) return false; // multicast / reserved
        }
        else if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal || addr.IsIPv6Multicast)
            {
                return false;
            }
            // fc00::/7 unique local
            if ((bytes[0] & 0xFE) == 0xFC) return false;
        }

        return true;
    }
}
