# Copilot Prompt Kit (reuse these)
> Always assume context from #/docs/product-brief.md unless I say otherwise.

## Implement feature
@workspace Read #/docs/product-brief.md. Implement {feature}. 
Target net9.0, DI-first. Keep Domain clean (no infra). 
Show full files or precise diffs. Include DI registrations and package refs.

## Add tests
@tests Read #/docs/product-brief.md and the target file. 
Create nUnit tests covering success, edge, malformed inputs. Deterministic, no external calls. 
Generate files under /tests/{Project}.Tests.

## Wire DI
@workspace Register services in Program.cs:
- {registrations with lifetimes}
Show exact code blocks for Program.cs and any Options classes.

## Fix build
@workspace Here is the error: ```{build error text}```. 
Provide minimal changes to fix. Show diffs and any missing package references.

## Parser upgrade
@workspace Extend Regex parser to handle: 
- SL/Stoploss variations, comma decimals, entry ranges (A-B), TP list variations (TP1:, TPs:, emojis).
- Return NormalizedSignal with hash dedupe key.
Add 8 unit tests.

## Connector scaffold
@workspace Create OANDA practice connector with PlaceMarketOrderAsync(symbol, side, units, sl?, tp?). 
Use HttpClientFactory, options binding. Include DTOs and error handling with logging. 
Provide Program.cs DI and appsettings.json example.

## Telemetry
@workspace Add OpenTelemetry + App Insights traces for: telegram_receive, parse, rules, command_emit, broker_ack. 
Add correlation ids and log structured properties.

## SaaS enablement
@workspace Add Azure AD B2C (JWT) auth to API endpoints /tenants/* and /channels/*. 
Add Swagger security scheme and an [Authorize] sample endpoint.

