namespace GeolocationService.Core.Exceptions;

public sealed class ProviderTransientException : Exception
{
    public string ProviderName { get; }

    public ProviderTransientException(string providerName, string message, Exception? inner = null)
        : base(message, inner)
    {
        ProviderName = providerName;
    }
}
