# Roadmap & Task List - AgentScope

## Fase 1: Autenticazione, Utenti e Multi-Tenancy
Questa fase prepara la sicurezza per distinguere i dati dei vari clienti.
- [ ] 1.1 Implementare ASP.NET Core Identity in locale + JWT.
  - [ ] 1.1.1 Configurare IdentityDbContext in `AgentScope.Infrastructure` e mappare l'entità User.
  - [ ] 1.1.2 Configurare l'autenticazione JWT in `AgentScope.Api` (Settings, Services, Middleware).
  - [ ] 1.1.3 Creare un `AuthController` con endpoint asincroni per Scelta (Login) e Registrazione (Register) che ritornano un Token JWT, utilizzando i pattern `MonadicSharp`.
- [ ] 1.2 Configurare il sistema di Login sulla web app Blazor (AuthenticationStateProvider custom per leggere il JWT).
- [ ] 1.3 Creare logica di rilascio `API Key` sicure per l'ingestion API (per l'agent AI del cliente).
- [ ] 1.4 Modificare `ApiKeyMiddleware.cs` per validare le chiavi reale contro il Database in Cache (Redis).

## Fase 2: Ingestion API & Data Pipeline (Core)
Per la raccolta e archiviazione delle trace e telemetrie degli AI Agent dei clienti.
- [ ] 2.1 Definire i modelli Entity Framework (Traces, Spans, Metrics) su Postgres.
- [ ] 2.2 Esporre gli Endpoints API (gRPC o Http POST) in `AgentScope.Api`.
- [ ] 2.3 Gestire Code / Batching in memoria per alte performance d'ingestion e scrivere in db asincrono.

## Fase 3: Frontend & Dashboard
Costruire l'interfaccia protetta dopo la landing page.
- [ ] 3.1 Implementare Layout con Sidebar (Agenti, Tracce, Metriche).
- [ ] 3.2 Visualizzare Grafici MudBlazor per le metriche (chiamate LLM, token, costi).
- [ ] 3.3 Elenco dettagliato delle tracce "Agent" con timeline ed estrazione dei payload in formato JSON / chat.

## Fase 4: Monetizzazione & Integrazioni
Rendere il prodotto vendibile tramite sottoscrizioni.
- [ ] 4.1 Creare il database/tabelle per gestire le Sottoscrizioni/Usage (Account, Limiti token/tracce per piano).
- [ ] 4.2 Integrare Stripe Billing (Webhook API e link di checkout Frontend).
- [ ] 4.3 Logica di rate-limiting avanzata (Blocco API all'esaurimento limiti di piano cliente).

## Fase 5: CI/CD & Deploy in Produzione
Pushare in produzione usando l'infrastruttura approvata.
- [ ] 5.1 Adattare Docker Compose o Dockerfile/Vars per Railway.app o Render.com.
- [ ] 5.2 Test Deployment in Staging.
- [ ] 5.3 Impostare dominio custom e SSL.
