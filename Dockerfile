# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

# Restore — layer-cached unless .csproj or package files change
COPY NuGet.config .
COPY packages/ packages/
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
RUN dotnet publish src/AgentScope.Api/AgentScope.Api.csproj -c Release -o /out/api
RUN dotnet publish src/AgentScope.Web/AgentScope.Web.csproj -c Release -o /out/web

# ── API runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
EXPOSE 8080

USER app
COPY --from=build --chown=app:app /out/api .
ENTRYPOINT ["dotnet", "AgentScope.Api.dll"]

# ── Web runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web
WORKDIR /app
EXPOSE 8080

USER app
COPY --from=build --chown=app:app /out/web .
ENTRYPOINT ["dotnet", "AgentScope.Web.dll"]
