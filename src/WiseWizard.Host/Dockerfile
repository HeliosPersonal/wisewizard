# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# wisewizard — single deployable unit (bot + broker session + nightly pipeline).
# Multi-stage build. Context is the repository root
# (docker build -f src/WiseWizard.Host/Dockerfile .). Central Package Management
# means Directory.*.props + nuget.config must be present before `dotnet restore`.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: copy the solution + build/CPM props + nuget.config + all project
# files first so `dotnet restore` caches independently of source changes.
COPY WiseWizard.slnx Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/ ./src/
RUN dotnet restore src/WiseWizard.Host/WiseWizard.Host.csproj

# Publish framework-dependent (portable; small image via the aspnet runtime base).
RUN dotnet publish src/WiseWizard.Host/WiseWizard.Host.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as the non-root `app` user shipped by the aspnet image (uid 1654).
USER app

# Kestrel listens on 8080 (matches the k8s Service + probes).
ENV ASPNETCORE_URLS=http://+:8080
# Domain state lives in PostgreSQL (ADR-0007). The connection string comes from
# Infisical at runtime (CONNECTIONSTRINGS__WISEWIZARD → ConnectionStrings:WiseWizard); no
# local database file and no writable volume are needed.
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "WiseWizard.Host.dll"]
