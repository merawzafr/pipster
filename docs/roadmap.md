# Roadmap

## Phase 1 – MVP (dev-only, in-memory) ✅ end-to-end slice
1. Contracts: NormalizedSignal, TradeCommand.
2. Parser v1: RegexSignalParser (+ xUnit tests).
3. InMemoryBus + Workers:
   - Telegram worker (stub message) → Publish signal → Increment state.
   - Executor worker → Convert to TradeCommand (TP1) → Dummy connector.
4. API: /state/health + Swagger.
5. Observability: Serilog + correlation ids.

**Exit criteria:** run solution; signal flows → trade command; /state/health increments.

## Phase 2 – Paper Trading (Beta private)
1. Telegram integration (Telegram.Bot long-poll):
   - Read configured chats (appsettings.json), dedupe by hash.
2. Rules & Risk (minimal):
   - Whitelist symbols, session window, fixed units, slippage guard.
3. OANDA Practice connector (REST):
   - Market order, SL/TP (when supported), error handling, retries (Polly).
4. Metrics:
   - end-to-end latency, success %, per-channel counts.

**Exit criteria:** real Telegram messages place paper trades; latency p95 < 1.5s.

## Phase 3 – Azure hardening
1. Swap InMemoryBus → Azure Service Bus (Queues/Topics).
2. Storage:
   - Azure SQL (Tenants/Channels/Rules), Cosmos DB (Signals/Events).
3. Hosting:
   - Azure Container Apps for workers, API; Front Door; APIM.
4. OpenTelemetry + App Insights traces; alerts.

**Exit criteria:** stable on Azure; restart-safe; DLQs; dashboards.

## Phase 4 – SaaS enablement (Public Beta)
1. AuthZ: Azure AD B2C JWT for API; per-tenant claims.
2. Plans/Quotas: Stripe checkout + webhooks; enforce limits in APIM and app.
3. Admin UI: minimal SPA for channels/rules/status.
4. Audit & Exports: Blob immutability; CSV export endpoints.

**Exit criteria:** onboard external testers; billing live; ToS & disclaimers.

## Phase 5 – GA (Market-ready)
1. Second broker connector (choose REST-friendly).
2. Resilience: chaos tests; failover playbooks; backup/restore drills.
3. Docs: Swagger polished; “Getting started” guide; support email.
4. SLO validation and alert runbooks.

**Exit criteria:** GA announcement; paywall on; support process active.
