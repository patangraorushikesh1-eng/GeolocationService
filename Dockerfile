# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GeolocationService.sln .
COPY global.json .
COPY src/GeolocationService.Api/GeolocationService.Api.csproj   src/GeolocationService.Api/
COPY src/GeolocationService.Core/GeolocationService.Core.csproj src/GeolocationService.Core/
COPY tests/GeolocationService.Tests/GeolocationService.Tests.csproj tests/GeolocationService.Tests/

RUN dotnet restore src/GeolocationService.Api/GeolocationService.Api.csproj

COPY . .
RUN dotnet publish src/GeolocationService.Api/GeolocationService.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Non-root user is built into the aspnet:8.0 image.
USER app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "GeolocationService.Api.dll"]
