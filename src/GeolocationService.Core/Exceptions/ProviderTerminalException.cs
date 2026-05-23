namespace GeolocationService.Core.Exceptions;

public sealed class ProviderTerminalException : Exception
{
    public string ProviderName { get; }

    public ProviderTerminalException(string providerName, string message, Exception? inner = null)
        : base(message, inner)
    {
        ProviderName = providerName;
    }
}
