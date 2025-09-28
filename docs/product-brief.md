# Pipster – Product Brief (SaaS)
**One-liner:** Turn Telegram trading signals into real trades with cloud-speed, safety, and control.

## Target users
- Solo traders and signal subscribers who want auto-execution.
- Small prop/communities managing multiple Telegram channels.

## Core value
- **Instant execution** from Telegram to broker.
- **No VPS scripts**: fully hosted SaaS with rules & risk controls.
- **Transparent audit** and per-channel controls.

## Scope (V1, Market-Ready)
- **Ingest**: Telegram channels (private/public) via bot/user session.
- **Parsing**: Robust regex + profiles; supports BUY/SELL, entry, SL, TP1..TPn, ranges and commas.
- **Rules & Risk**: Whitelist symbols, sessions, fixed/% equity sizing, max exposure, slippage guard, dedupe.
- **Execution**: At least 2 brokers (paper + one live REST): OANDA Practice + (second connector placeholder).
- **SaaS**: Multi-tenant (B2C auth), plans/quotas, Stripe checkout + webhooks, usage metering.
- **Ops**: Observability (App Insights), audit logs, retries, DLQs, idempotency, status dashboard.
- **Security/Compliance**: Managed identities, Key Vault, least privilege, data-at-rest encryption, ToS/Disclaimers.
- **Latency SLO**: TG receive → broker POST p95 < 1.5s; end-to-end success p99.5 ≥ 99%.

## Nice-to-haves (post-GA)
- Backtesting/replay, webhooks to Discord/Slack, more brokers, rules DSL UI.
- Geo residency options, SOC2 posture.

## Architecture (summary)
- .NET 9: API (ASP.NET Core), Workers (Telegram, Executor), Domain/Application/Infra/Connectors/Shared.
- Messaging: start InMemory → Azure Service Bus (Queues/Topics).
- Storage: Config in Azure SQL; Signals/Events in Cosmos DB; Audit in Blob.
- Hosting: Azure Container Apps + Front Door + APIM + AAD B2C.

## Definition of Done for GA
- Two connectors working (paper + real REST).
- End-to-end tests and chaos tests on queues.
- Billing + plan enforcement live.
- Incident runbooks and alerting.
- Swagger documented, versioned APIs.

