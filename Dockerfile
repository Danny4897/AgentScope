# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Restore — layer-cached unless .csproj files change
COPY Directory.Build.props .
COPY src/AgentScope.Domain/AgentScope.Domain.csproj             src/AgentScope.Domain/
COPY src/AgentScope.Infrastructure/AgentScope.Infrastructure.csproj src/AgentScope.Infrastructure/
COPY src/AgentScope.Api/AgentScope.Api.csproj                   src/AgentScope.Api/
COPY src/AgentScope.Web/AgentScope.Web.csproj                   src/AgentScope.Web/
COPY src/AgentScope.Sdk/AgentScope.Sdk.csproj                   src/AgentScope.Sdk/
RUN dotnet restore src/AgentScope.Api/AgentScope.Api.csproj
RUN dotnet restore src/AgentScope.Web/AgentScope.Web.csproj

# Build
COPY . .
RUN dotnet publish src/AgentScope.Api/AgentScope.Api.csproj -c Release -o /out/api --no-restore
RUN dotnet publish src/AgentScope.Web/AgentScope.Web.csproj -c Release -o /out/web --no-restore

# ── API runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS api
WORKDIR /app
EXPOSE 8080

RUN adduser -D -u 1001 appuser && chown -R appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /out/api .
ENTRYPOINT ["dotnet", "AgentScope.Api.dll"]

# ── Web runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS web
WORKDIR /app
EXPOSE 8080

RUN adduser -D -u 1001 appuser && chown -R appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /out/web .
ENTRYPOINT ["dotnet", "AgentScope.Web.dll"]
