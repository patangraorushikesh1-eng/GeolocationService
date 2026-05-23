using GeolocationService.Core.Abstractions;
using GeolocationService.Core.Providers;
using GeolocationService.Core.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeolocationService.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGeolocationProvider<TProvider, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TProvider : class, IGeolocationProvider
        where TOptions : ProviderOptionsBase, new()
    {
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.ApiKey) && !string.IsNullOrWhiteSpace(o.BaseUrl)),
                $"{sectionName}: ApiKey and BaseUrl are required when Enabled is true.")
            .ValidateOnStart();

        services
            .AddHttpClient<TProvider>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<TOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
                }
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) =>
                PollyPolicies.RetryOnceOnTransient(sp.GetRequiredService<ILoggerFactory>()));

        services.AddSingleton<IGeolocationProvider>(sp => sp.GetRequiredService<TProvider>());

        return services;
    }
}
