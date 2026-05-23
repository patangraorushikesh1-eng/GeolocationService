# Geolocation Fallback Service

A C# / ASP.NET Core 8 Web API that resolves an IP address into a standardized geolocation response using three external providers — **Geoapify**, **IPStack**, and **IP2Location** — with **round-robin** provider selection, **retry-once-then-fallback** semantics, and a controlled failure response when every provider fails.

Two endpoint forms are available:

| Route | Behavior |
|---|---|
| `GET /api/geolocation/{ip}` | Resolves the IP given in the path. **Preferred for local testing.** |
| `GET /api/geolocation`      | Auto-resolves the caller's IP from the request (honors `X-Forwarded-For` via `UseForwardedHeaders`). |

> **Local-dev note:** when you call `GET /api/geolocation` from the same machine that runs the service, the caller IP is loopback (`127.0.0.1` / `::1`). Loopback isn't a public address — no provider can geolocate it — so the service returns a `400 InvalidIpAddress`. Use the `/api/geolocation/{ip}` form locally.

## Project structure

The solution is split into three projects, each with a single responsibility:

```
GeolocationService/
├── GeolocationService.sln
├── global.json                 # SDK pin (rolls forward to latest installed feature band)
├── Dockerfile                  # multi-stage build → non-root aspnet:8.0 runtime image
├── README.md
├── scripts/
│   └── smoke-test.ps1          # end-to-end probe against a running instance
├── src/
│   ├── GeolocationService.Core/        # ── class library, no ASP.NET dependency
│   │   ├── Abstractions/               #   IGeolocationProvider, IProviderSelector, IGeolocationOrchestrator
│   │   ├── Models/                     #   GeolocationResult, Coordinates, GeolocationOutcome (discriminated union)
│   │   ├── Exceptions/                 #   InvalidIp / ProviderTransient / ProviderTerminal
│   │   ├── Orchestration/              #   GeolocationOrchestrator + RoundRobinProviderSelector
│   │   ├── Resilience/                 #   PollyPolicies (retry-once on 5xx/408/429)
│   │   ├── Providers/
│   │   │   ├── ProviderOptionsBase.cs  #   shared Enabled/ApiKey/BaseUrl/TimeoutSeconds
│   │   │   ├── Geoapify/               #   Options + Response DTO + Provider
│   │   │   ├── IpStack/
│   │   │   └── Ip2Location/
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs   # AddGeolocationProvider<TProvider, TOptions>
│   │
│   └── GeolocationService.Api/         # ── ASP.NET Core minimal-API host
│       ├── Program.cs                  #   composition root: Serilog, DI, middleware, endpoints
│       ├── appsettings.json            #   BaseUrls + Enabled flags (NO api keys)
│       ├── Endpoints/                  #   /api/geolocation routes + ErrorResponse shape
│       ├── Middleware/                 #   CorrelationIdMiddleware (X-Correlation-Id)
│       ├── HealthChecks/               #   /health — fails if zero providers enabled
│       └── Services/                   #   IpValidation, CallerIpResolver
│
└── tests/
    └── GeolocationService.Tests/       # ── xUnit + FluentAssertions + Moq + WebApplicationFactory
        ├── Fakes/                      #   FakeGeolocationProvider, StubHttpMessageHandler
        ├── Orchestration/              #   round-robin + orchestrator tests
        ├── Providers/                  #   per-provider mapping + retry tests
        └── Api/                        #   end-to-end endpoint tests via TestServer
```

**Dependency direction is strictly one-way:** `Tests → Api → Core`. `Core` references no ASP.NET types, so the provider/orchestration logic could be reused from a console host, a worker service, or a gRPC front-end without changes.

### Key files to read first

If you only have five minutes, read these in order:

1. [`Program.cs`](src/GeolocationService.Api/Program.cs) — the whole composition root in ~50 lines.
2. [`GeolocationOrchestrator.cs`](src/GeolocationService.Core/Orchestration/GeolocationOrchestrator.cs) — the fallback loop. Note there is zero provider-specific code.
3. [`RoundRobinProviderSelector.cs`](src/GeolocationService.Core/Orchestration/RoundRobinProviderSelector.cs) — thread-safe rotation via `Interlocked.Increment`.
4. [`ServiceCollectionExtensions.cs`](src/GeolocationService.Core/DependencyInjection/ServiceCollectionExtensions.cs) — the generic registration that makes adding a provider a one-liner.
5. Any one provider, e.g. [`GeoapifyProvider.cs`](src/GeolocationService.Core/Providers/Geoapify/GeoapifyProvider.cs) — the per-provider pattern: send → classify → map.

## Configuration

API keys are **never** committed and **never** placed in `appsettings.json`.

```bash
cd src/GeolocationService.Api
dotnet user-secrets set "Providers:Geoapify:ApiKey"    "<key>"
dotnet user-secrets set "Providers:IPStack:ApiKey"     "<key>"
dotnet user-secrets set "Providers:IP2Location:ApiKey" "<key>"
```

Or via environment variables (note the double underscore):

```bash
export Providers__Geoapify__ApiKey="<key>"
export Providers__IPStack__ApiKey="<key>"
export Providers__IP2Location__ApiKey="<key>"
```

## Run

```bash
dotnet restore
dotnet run --project src/GeolocationService.Api
```

## Test

```bash
dotnet test
```

## Smoke test (end-to-end against a running instance)

`scripts/smoke-test.ps1` exercises every endpoint of a running service and prints a colored PASS/FAIL line per case. It hits:

- `GET /health`
- `GET /api/geolocation/{ip}` for a curated list of public IPv4 and IPv6 addresses (expecting `200`)
- `GET /api/geolocation/{ip}` for loopback, RFC 1918 private, link-local, multicast, and unparseable inputs (expecting `400` with no provider call)

The script exits with the count of failed checks, so it works as-is in CI.

### How to run it

1. Start the API in one terminal:
   ```powershell
   dotnet run --project src/GeolocationService.Api
   ```
   Note the URL it binds to (e.g. `http://localhost:5000` or the HTTPS port from `launchSettings.json`).

2. In a second terminal, run the script:
   ```powershell
   cd scripts
   .\smoke-test.ps1
   ```

   To target a non-default URL:
   ```powershell
   .\smoke-test.ps1 -BaseUrl https://localhost:59844
   ```

3. (Optional) If you hit a PowerShell execution-policy block:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\smoke-test.ps1
   ```

> **Note:** the happy-path cases call the real Geoapify / IPStack / IP2Location APIs, so make sure your API keys are configured (`dotnet user-secrets` or env vars) before running the script. Without keys, every provider returns `502` and the happy-path rows will report FAIL — the validation-failure rows will still pass.

## Adding a new provider

1. Create `src/GeolocationService.Core/Providers/Foo/` with `FooOptions.cs`, `FooResponse.cs`, `FooProvider.cs`.
2. Add the section to `appsettings.json` and the API key to user-secrets.
3. Register in `Program.cs`:
   ```csharp
   .AddGeolocationProvider<FooProvider, FooOptions>(builder.Configuration, "Providers:Foo")
   ```

That's it — the orchestrator picks it up via `IEnumerable<IGeolocationProvider>`.

## Design decisions

A few choices worth calling out:

### Polly handles retry, the orchestrator handles fallback
The single-retry policy lives on each typed `HttpClient` (`PollyPolicies.RetryOnceOnTransient`), not in the orchestrator. This keeps the two concerns cleanly separated:

- **Provider level (Polly):** transient HTTP noise — 5xx, 408, 429, socket errors. Retried once with a 200 ms back-off *before* the exception surfaces.
- **Orchestrator level:** provider-to-provider fallback. By the time the orchestrator sees a failure, the provider has already exhausted its retry budget, so the orchestrator's loop stays a simple `foreach` with zero retry bookkeeping.

This also means the retry policy is configurable per provider (via DI) without touching the orchestrator.

### `Interlocked.Increment` instead of a `lock`
The round-robin counter is incremented on every request, so it's on the hot path. `Interlocked.Increment` is a single lock-free CPU instruction (`LOCK XADD`) — no kernel transition, no contention, no risk of a forgotten `Monitor.Exit`. A `lock` would work correctly but serialize every request through a monitor for what is effectively one integer increment. The `((next % len) + len) % len` dance handles the eventual overflow of `long` back into negative territory without ever needing to reset the counter.

### `internal sealed` provider DTOs
`GeoapifyResponse`, `IpStackResponse`, and `Ip2LocationResponse` are deliberately `internal sealed`:

- **`internal`** — they're deserialization shapes, not part of the public contract. Only the matching `*Provider` class needs them, so they can't leak out of the assembly and accidentally end up in a public API or log payload.
- **`sealed`** — no inheritance is intended, and sealing lets the JIT devirtualize calls and produces marginally smaller IL. It also documents intent: "this is a wire format, not an extension point."

The only type that crosses the assembly boundary is the unified `GeolocationResult`, which keeps the public surface area tight.

### `ValidateOnStart()` for provider options
Each provider is registered with `.ValidateDataAnnotations().Validate(...).ValidateOnStart()`. If a provider is `Enabled=true` but `ApiKey` or `BaseUrl` is blank, the host **fails to start** with a clear error — rather than booting successfully and then returning 502s at 3 a.m. when the first request arrives. Misconfiguration becomes a deploy-time failure instead of a runtime one, which is exactly the trade-off you want for a service whose whole job is to call external APIs.

---

## Notes for the reviewer

### Tech stack at a glance

| Concern | Choice |
|---|---|
| Runtime / SDK | .NET 8 (`net8.0`); `global.json` rolls forward to latest installed feature band |
| Web framework | ASP.NET Core 8 Minimal APIs |
| HTTP client | `IHttpClientFactory` typed clients, one per provider |
| Resilience | Polly (`Microsoft.Extensions.Http.Polly`) — single retry on 5xx / 408 / 429 |
| Logging | Serilog → console + rolling daily file (`logs/geo-*.log`) with `X-Correlation-Id` enrichment |
| Config | `IOptions<T>` + `ValidateDataAnnotations` + `ValidateOnStart` |
| Secrets | `dotnet user-secrets` (dev) / environment variables (prod). Never `appsettings.json` |
| Docs | Swagger UI at `/swagger` in Development |
| Health | `/health` — fails if zero providers enabled |
| Testing | xUnit + FluentAssertions + Moq + `WebApplicationFactory<Program>` |

### Endpoint contract

| Status | When | Body shape |
|---|---|---|
| `200 OK` | A provider returned a usable result | `GeolocationResult` (country, state, city, zipcode, coordinates, time_zone, isp, currency) |
| `400 Bad Request` | IP fails pre-validation OR every provider reported the IP as invalid | `ErrorResponse { error: "InvalidIpAddress", message, correlation_id }` |
| `502 Bad Gateway` | Every enabled provider failed after its own retry | `ErrorResponse { error: "AllProvidersFailed", message, attempted_providers, correlation_id }` |

Every response carries an `X-Correlation-Id` header. If the request supplied one, it is reused; otherwise a fresh GUID is generated and threaded through every log line for that request.

### What gets rejected before any provider is called

`IpValidation.IsPublic` (used by both endpoints) rejects with `400` and **does not call any provider** for:

- Unparseable strings (`not-an-ip`)
- IPv4 loopback (`127.0.0.0/8`) and IPv6 loopback (`::1`)
- RFC 1918 private (`10/8`, `172.16/12`, `192.168/16`)
- Link-local (`169.254/16`, IPv6 `fe80::/10`)
- Multicast / reserved (`>= 224.0.0.0`, IPv6 multicast)
- IPv6 site-local and unique-local (`fc00::/7`)
- `0.0.0.0/8`

This keeps the upstream APIs from being billed for requests they can't answer.

### Test inventory (28 tests, all passing, zero live HTTP)

| File | Covers |
|---|---|
| `Orchestration/RoundRobinProviderSelectorTests.cs` | rotation order, disabled-provider exclusion, empty case, concurrent fairness (3000 parallel requests) |
| `Orchestration/GeolocationOrchestratorTests.cs` | success, transient fallback, terminal fallback, all-fail, invalid-IP short-circuit, critical-log assertion, new-fake-provider extensibility |
| `Providers/GeoapifyProviderTests.cs` | response mapping + Polly retry-then-succeed on `503` |
| `Providers/IpStackProviderTests.cs` | mapping, `code: 106` → `InvalidIpException`, other codes → `ProviderTerminalException` |
| `Providers/Ip2LocationProviderTests.cs` | mapping, `error_code: 10001` (HTTP 400 body) → `InvalidIpException`, other codes → `ProviderTerminalException` |
| `Api/GeolocationEndpointTests.cs` | end-to-end via `TestServer`: 400 for non-public IPs, 200 for fake success, 502 with `attempted_providers`, no `ApiKey` leakage, `/health` |

Run with `dotnet test` — every external call is mocked at the `HttpMessageHandler` level or replaced via `WebApplicationFactory.ConfigureTestServices`. No internet access required.

### Quick reviewer checklist

```powershell
# 1. From the solution root:
dotnet restore
dotnet build                 # clean build under <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
dotnet test                  # 28/28 should pass in ~12 s

# 2. (Optional) end-to-end against the real providers — requires API keys
cd src/GeolocationService.Api
dotnet user-secrets set "Providers:Geoapify:ApiKey"    "<key>"
dotnet user-secrets set "Providers:IPStack:ApiKey"     "<key>"
dotnet user-secrets set "Providers:IP2Location:ApiKey" "<key>"
cd ../..
dotnet run --project src/GeolocationService.Api

# 3. In a second terminal:
cd scripts
.\smoke-test.ps1 -BaseUrl https://localhost:59844
```

Sample successful response:

```json
{
  "country": "United States",
  "state": "California",
  "city": "Los Angeles",
  "zipcode": "90006",
  "coordinates": { "lat": 34.0476, "lng": -118.2923 },
  "time_zone": "America/Los_Angeles",
  "isp": "Los Angeles Department of Water & Power",
  "currency": "USD"
}
```

### Requirements ↔ implementation map

| PDF requirement | Where it lives |
|---|---|
| Three named providers | `src/GeolocationService.Core/Providers/{Geoapify,IpStack,Ip2Location}/` |
| Keys not hardcoded | `ProviderOptionsBase.ApiKey` bound via `IConfiguration`; `UserSecretsId` set in API csproj |
| Unified output shape | `Models/GeolocationResult.cs` + per-provider `Map(...)` methods |
| Round-robin | `Orchestration/RoundRobinProviderSelector.cs` (`Interlocked.Increment`) |
| Concurrency-safe round-robin | `Round_robin_distribution_is_balanced_under_concurrent_load` test |
| Retry once, then fallback | `Resilience/PollyPolicies.cs` + `Orchestration/GeolocationOrchestrator.cs` |
| Stop on first success | `GeolocationOrchestrator.ResolveAsync` returns on `Success` |
| Controlled failure + critical log + attempted providers | `GeolocationOrchestrator` (LogCritical) + `Endpoints/GeolocationEndpoint` (502 + `attempted_providers`) |
| No API keys / raw errors leaked | `ErrorResponse` contains only provider names + correlation id; test asserts body does not contain `"ApiKey"` |
| Extensibility (new provider = one DI line) | `DependencyInjection/ServiceCollectionExtensions.AddGeolocationProvider` |
| No provider-specific logic in orchestrator | `GeolocationOrchestrator` depends only on `IEnumerable<IGeolocationProvider>` |
| Invalid IP handling | `Services/IpValidation.cs` (pre-call) + `InvalidIpException` short-circuit (provider-reported) |
| Tests without live APIs | `Fakes/StubHttpMessageHandler.cs` + `ConfigureTestServices` swap-in |
