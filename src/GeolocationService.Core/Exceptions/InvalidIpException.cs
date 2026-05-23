namespace GeolocationService.Core.Exceptions;

public sealed class InvalidIpException : Exception
{
    public InvalidIpException(string message) : base(message) { }
}
