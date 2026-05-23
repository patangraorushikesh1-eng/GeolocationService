using GeolocationService.Api.Endpoints;
using GeolocationService.Api.HealthChecks;
using GeolocationService.Api.Middleware;
using GeolocationService.Api.Services;
using GeolocationService.Core.Abstractions;
using GeolocationService.Core.DependencyInjection;
using GeolocationService.Core.Orchestration;
using GeolocationService.Core.Providers.Geoapify;
using GeolocationService.Core.Providers.Ip2Location;
using GeolocationService.Core.Providers.IpStack;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

const string logTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] [cid:{CorrelationId}] {Message:lj}{NewLine}{Exception}";

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: logTemplate)
    .WriteTo.File("logs/geo-.log", outputTemplate: logTemplate, rollingInterval: RollingInterval.Day));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services
    .AddGeolocationProvider<GeoapifyProvider,    GeoapifyOptions>   (builder.Configuration, "Providers:Geoapify")
    .AddGeolocationProvider<IpStackProvider,     IpStackOptions>    (builder.Configuration, "Providers:IPStack")
    .AddGeolocationProvider<Ip2LocationProvider, Ip2LocationOptions>(builder.Configuration, "Providers:IP2Location");

builder.Services.AddSingleton<IProviderSelector, RoundRobinProviderSelector>();
builder.Services.AddSingleton<IGeolocationOrchestrator, GeolocationOrchestrator>();
builder.Services.AddSingleton<ICallerIpResolver, CallerIpResolver>();

builder.Services
    .AddHealthChecks()
    .AddCheck<ProvidersConfiguredHealthCheck>("providers");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGeolocationEndpoint();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
