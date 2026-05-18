# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching for NuGet restore)
COPY PartnerIntegrationBFF.sln .
COPY src/PartnerIntegration.Domain/PartnerIntegration.Domain.csproj             src/PartnerIntegration.Domain/
COPY src/PartnerIntegration.Application/PartnerIntegration.Application.csproj   src/PartnerIntegration.Application/
COPY src/PartnerIntegration.Infrastructure/PartnerIntegration.Infrastructure.csproj src/PartnerIntegration.Infrastructure/
COPY src/PartnerIntegration.API/PartnerIntegration.API.csproj                   src/PartnerIntegration.API/
COPY tests/PartnerIntegration.Tests/PartnerIntegration.Tests.csproj             tests/PartnerIntegration.Tests/

RUN dotnet restore

# Copy all source and build
COPY . .
RUN dotnet build "src/PartnerIntegration.API/PartnerIntegration.API.csproj" -c Release -o /app/build

# ── Publish stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "src/PartnerIntegration.API/PartnerIntegration.API.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
RUN mkdir -p /app/logs && chown -R appuser:appgroup /app

COPY --from=publish /app/publish .
RUN chown -R appuser:appgroup /app

USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "PartnerIntegration.API.dll"]
