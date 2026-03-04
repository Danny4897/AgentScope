# AgentScope — Development Roadmap

> Stato aggiornato: 2026-03-04

---

## Legenda stato

| Simbolo | Significato |
|---|---|
| ✅ | Completato |
| ⬜ | Da fare |

---

## Fase 0 — Scaffold ✅

- ✅ `AgentScope.sln` con 8 progetti
- ✅ `Directory.Build.props`, `NuGet.config`, `global.json`
- ✅ `docker-compose.yml` — PostgreSQL 16 + Redis 7
- ✅ `Dockerfile` — multi-stage (target `api` + `web`)
- ✅ `.github/workflows/ci.yml`

---

## Fase 1 — Domain + Infrastructure ✅

- ✅ Entità: `User`, `Application`, `Subscription`, `Trace`, `Span`, `AgentEvent`
- ✅ Port interfaces: `IUserRepository`, `IApplicationRepository`, `ITraceRepository`, `ISubscriptionRepository`
- ✅ `DomainErrors` — factory tipizzata, zero throw
- ✅ `AgentScopeDbContext` + EF Core configurations (jsonb per OTLP attributes)
- ✅ 4 repository EF Core implementati
- ✅ `DependencyInjection.cs`
- ✅ 7 unit test dominio passano

---

## Fase 2 — API Ingestione OTLP ⬜

**Obiettivo:** ricevere trace da SDK → validare → persistere su DB.

- ⬜ EF Core migration iniziale (`dotnet ef migrations add Initial`)
- ⬜ `TraceController` — `POST /v1/traces` (OTLP/HTTP JSON)
- ⬜ `MetricsController` — `POST /v1/metrics`
- ⬜ `TraceIngestionAgent` (MonadicSharp.Agents) — parsing + validazione + persist
- ⬜ `MetricsAggregationAgent` — aggrega per finestre temporali (1min, 5min, 1h)
- ⬜ `ApiKeyMiddleware` — estrae `x-api-key`, risolve `Application`
- ⬜ Rate limiting per piano (Free: 10k/mese, Pro: 500k, Business: illimitato)
- ⬜ Test integrazione: `POST /v1/traces` → verifica record su DB

**Test manuale con curl dopo questa fase:**
```bash
curl -X POST http://localhost:5000/v1/traces \
  -H "x-api-key: <tua-api-key>" \
  -H "Content-Type: application/json" \
  -d '{"resourceSpans": [...]}'
```

---

## Fase 3 — Auth + Subscription ⬜

- ⬜ ASP.NET Core Identity (tabelle utenti + hashing password)
- ⬜ JWT — generazione + `[Authorize]` sugli endpoint
- ⬜ `AuthController` — `POST /auth/register`, `/auth/login`, `/auth/refresh`
- ⬜ `ApiKeyController` — CRUD chiavi API per applicazione
- ⬜ Stripe integration:
  - ⬜ Checkout session (`POST /billing/checkout`)
  - ⬜ Webhook handler (`POST /billing/webhook`) — aggiorna `Subscription`
  - ⬜ Portal session (`POST /billing/portal`)
- ⬜ Tier enforcement middleware (quota eventi mensile)

---

## Fase 4 — Dashboard Blazor SSR ⬜

- ⬜ Layout MudBlazor (sidebar + appbar)
- ⬜ Pagina **Overview** — metriche aggregate (failure rate, latenza media, trace totali)
- ⬜ Pagina **Traces** — tabella paginata con filtri
- ⬜ Pagina **Trace Detail** — waterfall view span gerarchico
- ⬜ Pagina **Metrics** — grafici latenza/throughput (MudChart)
- ⬜ Pagina **Circuit Breakers** — stati live
- ⬜ Login / Register page
- ⬜ Route guard (redirect → login se non autenticato)

---

## Fase 5 — AgentScope.Sdk NuGet ⬜

- ⬜ `AgentScopeCollector` — OTLP exporter verso AgentScope
- ⬜ Integrazione `MonadicSharp.Telemetry`
- ⬜ Integrazione `Microsoft.Extensions.AI` (mercato più ampio)
- ⬜ App di esempio end-to-end
- ⬜ Pubblicazione `AgentScope.Sdk` su NuGet.org

---

## Fase 6 — Alert + Notification ⬜

- ⬜ `AlertRule` entity + soglie configurabili
- ⬜ `AlertEvaluationAgent` — valuta regole ogni N minuti
- ⬜ Email alert (Resend)
- ⬜ Slack webhook (piano Business)
- ⬜ Teams webhook (piano Business)

---

## Fase 7 — Landing Page + SEO ⬜

- ⬜ Pagina `/` — landing con hero, features, pricing
- ⬜ Pagina `/pricing` — dettaglio piani
- ⬜ Pagina `/docs` — quick start SDK + API reference
- ⬜ Blog Markdown per SEO
- ⬜ Meta tag OpenGraph + sitemap.xml

---

## Fase 8 — Deploy + Go Live ⬜

- ⬜ Deploy Railway.app (o Fly.io)
- ⬜ Dominio custom + HTTPS
- ⬜ Stripe live keys in environment
- ⬜ `MigrateAsync()` automatico all'avvio
- ⬜ Self-monitoring (AgentScope monitora se stesso)
- ⬜ Annuncio: Hacker News, r/dotnet, X/Twitter, LinkedIn

---

## Milestone testabilità

| Milestone | Fase | Cosa si può testare |
|---|---|---|
| **M1 — First trace** | Fase 2 | `curl POST /v1/traces` → record su DB |
| **M2 — Authenticated** | Fase 3 | Login, API key, quota enforcement |
| **M3 — Dashboard** | Fase 4 | Trace visibili in UI Blazor |
| **M4 — SDK live** | Fase 5 | App reale → trace in dashboard |
| **M5 — Go live** | Fase 8 | Produzione su dominio pubblico |

---

## Comandi utili

```bash
# Build
bash -c "export HOME=/home/danny && /home/danny/.dotnet/dotnet build /home/danny/AgentScope/AgentScope.sln -c Release"

# Test
bash -c "export HOME=/home/danny && /home/danny/.dotnet/dotnet test /home/danny/AgentScope/AgentScope.sln -c Release"

# EF Core migration (dopo Fase 2)
bash -c "export HOME=/home/danny && /home/danny/.dotnet/dotnet ef migrations add Initial \
  --project src/AgentScope.Infrastructure \
  --startup-project src/AgentScope.Api"

# Avvia infrastruttura locale
docker compose up -d postgres redis
```
