# AgentScope — AI Agent Observability for .NET

[![CI](https://github.com/Danny4897/AgentScope/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/AgentScope/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AgentScope.Sdk.svg)](https://www.nuget.org/packages/AgentScope.Sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **See every agent. Trace every pipeline. Catch every failure — natively in .NET.**

AgentScope is the first AI agent observability platform built specifically for .NET.
Where tools like LangSmith and Helicone are Python-first,
AgentScope speaks fluent C# — built on [MonadicSharp](https://github.com/Danny4897/MonadicSharp.Framework), OpenTelemetry, and ASP.NET Core 8.

---

## Features

- **Pipeline visualization** — interactive waterfall view of agent/span hierarchies
- **Real-time metrics** — latency, throughput, failure rate, token usage per pipeline
- **CircuitBreaker monitoring** — live state transitions (Closed / Open / HalfOpen)
- **Result-type tracking** — surface `Result.Failure` values that Sentry/Bugsnag miss
- **Audit trail** — full chronological log of agent decisions (via MonadicSharp.Security)
- **Alerts** — email, Slack, Teams on failure-rate threshold breaches
- **SDK in 2 lines** — `dotnet add package AgentScope.Sdk` and configure

---

## Quick Start

### 1. Install the SDK

```bash
dotnet add package AgentScope.Sdk
```

### 2. Instrument your app

```csharp
// Program.cs
builder.Services.AddAgentScope(options =>
{
    options.Endpoint   = "https://app.agentscope.io";
    options.ApiKey     = Environment.GetEnvironmentVariable("AGENTSCOPE_API_KEY")!;
    options.ServiceName = "my-ai-pipeline";
});
```

That's it. AgentScope auto-discovers all `ActivitySource` traces in your app,
including MonadicSharp.Telemetry, Microsoft.Extensions.AI, and Semantic Kernel.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core 8 + MonadicSharp.Framework |
| Frontend | Blazor SSR + MudBlazor |
| Database | PostgreSQL 16 (MonadicSharp.Persistence + EF Core) |
| Cache | Redis (MonadicSharp.Caching) |
| Ingestione | OTLP/HTTP + MonadicSharp.Agents |
| Auth | ASP.NET Core Identity + JWT |
| Payments | Stripe |
| Deploy | Docker + Railway.app |

---

## Architecture

```
AgentScope/
├── src/
│   ├── AgentScope.Web/          ← Blazor SSR dashboard (MudBlazor)
│   ├── AgentScope.Api/          ← ASP.NET Core API + OTLP receiver
│   ├── AgentScope.Domain/       ← Entities, errors, ports (no deps)
│   ├── AgentScope.Infrastructure/  ← EF Core, Redis, OTLP pipeline
│   └── AgentScope.Sdk/          ← NuGet SDK for end-users
├── tests/
│   ├── AgentScope.Domain.Tests/
│   ├── AgentScope.Api.Tests/
│   └── AgentScope.Infrastructure.Tests/
├── docker-compose.yml
├── Dockerfile
└── .github/workflows/ci.yml
```

Dependency flow: `Web → Api → Infrastructure → Domain ← Sdk`

---

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for PostgreSQL + Redis)

### Start the stack

```bash
# 1. Start infrastructure
docker compose up -d postgres redis

# 2. Apply migrations
dotnet ef database update --project src/AgentScope.Infrastructure --startup-project src/AgentScope.Api

# 3. Run API
dotnet run --project src/AgentScope.Api

# 4. Run Web (separate terminal)
dotnet run --project src/AgentScope.Web
```

API: http://localhost:5000 | Web: http://localhost:5001 | Swagger: http://localhost:5000/swagger

### Run tests

```bash
dotnet test
```

---

## Deployment

### Railway.app (recommended)

```bash
railway login
railway init
railway add --service postgres
railway add --service redis
railway up
```

### Fly.io

```bash
fly launch --dockerfile Dockerfile
fly secrets set AGENTSCOPE_POSTGRES_URL="..."
fly deploy
```

---

## Pricing

| Plan | Price | Apps | Retention | Events/mo | Alerts |
|---|---|---|---|---|---|
| Free | $0 | 1 | 7 days | 10k | — |
| Pro | $19/mo | 5 | 30 days | 500k | Email |
| Business | $49/mo | Unlimited | 90 days | Unlimited | Slack/Teams |
| Enterprise | Custom | Unlimited | Custom | Unlimited | Custom |

---

## Contributing

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Open a pull request

Please follow the existing code style: Result-types everywhere, no `throw` in business logic.

---

## License

MIT © Danny4897 — see [LICENSE](LICENSE)

---

*Built with [MonadicSharp](https://github.com/Danny4897/MonadicSharp.Framework) — railway-oriented programming for .NET.*
