# Pipster - Architecture Documentation

## Overview

Pipster is a multi-tenant SaaS platform that automates forex/metals trading by processing signals from Telegram channels and executing trades through multiple broker integrations.

## Architecture Principles

### 1. Clean Architecture
The system follows clean architecture principles with clear separation of concerns:
- **Domain Layer**: Business entities and rules (no dependencies)
- **Application Layer**: Use cases and orchestration
- **Infrastructure Layer**: External integrations and implementations
- **Presentation Layer**: API endpoints and worker services

### 2. Multi-Tenancy
Complete tenant isolation at all levels:
- Separate Telegram client instances per tenant
- Tenant-scoped data access
- Per-tenant configurations and credentials
- Independent broker connections

### 3. Event-Driven Architecture
Components communicate via message bus:
- Loose coupling between services
- Asynchronous processing
- Horizontal scalability
- Easy migration from in-memory to distributed messaging

### 4. Pluggable Broker Integrations
Factory pattern enables adding brokers without code changes:
- Interface-based connector design
- Provider pattern for dependency injection
- Central factory for connector lifecycle
- Host-level registration only

## High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      TELEGRAM CLOUD                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     │ TDLib Protocol
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Telegram Worker Service                        │
│  - Multi-tenant client management                           │
│  - Session persistence                                      │
│  - Message observation                                      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     │ TelegramMessageReceived
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                    Message Bus                              │
│  (InMemoryBus → Azure Service Bus)                          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│          Telegram Message Handler Worker                    │
│  Pipeline: Idempotency → Validation → Parsing → Risk        │
└────────────────────┬────────────────────────────────────────┘
                     │
                     │ TradeCommand
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               Trade Executor Worker                         │
│  - Connector factory integration                            │
│  - Multi-broker support                                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
              ┌──────────────┐
              │   Brokers    │
              │  IG Markets  │
              │    OANDA     │
              │     IBKR     │
              └──────────────┘
```

## Project Structure

```
Pipster/
├── Pipster.Domain/              # Core business logic
│   ├── Entities/                # Aggregates and entities
│   ├── Repositories/            # Repository interfaces
│   └── Enums/                   # Domain enumerations
│
├── Pipster.Application/         # Use cases and orchestration
│   ├── Services/                # Application services
│   ├── Handlers/                # Message handlers
│   └── Parsing/                 # Signal parsing
│
├── Pipster.Infrastructure/      # External integrations
│   ├── Telegram/                # Telegram integration
│   ├── Messaging/               # Message bus
│   ├── Repositories/            # Repository implementations
│   ├── Idempotency/             # Deduplication
│   └── Connectors/              # Broker connector factory
│
├── Pipster.Shared/              # Cross-cutting contracts
│   └── Contracts/               # DTOs and interfaces
│
├── Pipster.Api/                 # REST API
│   └── Controllers/             # HTTP endpoints
│
├── Pipster.Workers.Telegram/    # Telegram background worker
├── Pipster.Workers.Executor/    # Trade execution worker
│
├── Pipster.Connectors.*/        # Broker-specific implementations
│   ├── IGMarkets/               # IG Markets connector
│   └── FXCM/                    # FXCM connector (placeholder)
│
└── Pipster.Host/                # Composition root
    └── Program.cs               # DI registration and startup
```

## Detailed Documentation

### Core Architecture
- [Layer Architecture](./layers.md) - Detailed explanation of each layer
- [Domain Model](./domain-model.md) - Entity relationships and business rules
- [Dependency Flow](./dependency-flow.md) - How dependencies flow between layers

### Message Processing
- [Message Flow](./message-flow.md) - End-to-end signal processing pipeline
- [Message Contracts](./message-contracts.md) - Event and command schemas

### Multi-Tenancy
- [Tenant Isolation](./tenant-isolation.md) - How tenant data is separated
- [Tenant Lifecycle](./tenant-lifecycle.md) - States and transitions

### Integration Patterns
- [Telegram Integration](./telegram-integration.md) - TDLib client architecture
- [Broker Connectors](./broker-connectors.md) - Pluggable connector pattern
- [Message Bus](./message-bus.md) - Event-driven communication

### Resilience & Performance
- [Resilience Patterns](./resilience-patterns.md) - Circuit breakers, retries, timeouts
- [Performance Optimization](./performance-optimization.md) - Caching, pooling, async
- [Scalability](./scalability.md) - Horizontal scaling strategies

### Data Management
- [Data Architecture](./data-architecture.md) - Repository pattern and storage
- [Caching Strategy](./caching-strategy.md) - Multi-level caching approach

## Design Decisions

### Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| Clean Architecture | Testability, maintainability, independence from frameworks |
| Event-Driven | Loose coupling, scalability, asynchronous processing |
| Factory Pattern for Brokers | Extensibility without modifying existing code |
| Multi-Tenant Telegram Clients | Isolation, rate limit avoidance, fault containment |
| Repository Pattern | Abstraction over data access, easier testing |
| Cached Configuration | Reduce database load for high-frequency reads |
| Session Persistence in Blob | Durable storage, cost-effective, scalable |

### Technology Choices

| Technology | Purpose | Rationale |
|------------|---------|-----------|
| .NET 9.0 | Runtime Framework | Latest LTS, modern C# features, performance |
| TDLib | Telegram Integration | Official library, full API support, session management |
| Azure Blob Storage | Session Storage | Durable, scalable, cost-effective |
| Redis | Idempotency & Cache | Fast, distributed, atomic operations |
| Polly | Resilience | Proven library, circuit breakers, retries |
| Dependency Injection | Composition | Built-in .NET support, testability |

## Quality Attributes

### Performance
- **Target**: p95 latency < 1.5s (Telegram → Broker)
- **Strategy**: Caching, connection pooling, async processing

### Scalability
- **Target**: Support 10,000+ tenants
- **Strategy**: Horizontal scaling, stateless workers, distributed caching

### Reliability
- **Target**: 99.5% success rate
- **Strategy**: Circuit breakers, retries, graceful degradation, health checks

### Security
- **Target**: SOC 2 compliance-ready
- **Strategy**: Encryption at rest, Key Vault, least privilege, tenant isolation

### Maintainability
- **Target**: Easy to understand and modify
- **Strategy**: Clean architecture, SOLID principles, comprehensive documentation

## System Context Diagram

```
┌──────────────┐
│   Telegram   │
│    Users     │
└──────┬───────┘
       │
       │ Posts signals
       ▼
┌──────────────────┐
│  Telegram Cloud  │
└──────┬───────────┘
       │
       │ TDLib Protocol
       ▼
┌──────────────────────────────────────────┐
│           PIPSTER PLATFORM                │
│                                           │
│  ┌─────────────┐      ┌──────────────┐   │
│  │  Telegram   │──────│   Message    │   │
│  │   Worker    │      │   Handler    │   │
│  └─────────────┘      └──────┬───────┘   │
│                              │           │
│                              ▼           │
│                       ┌──────────────┐   │
│                       │    Trade     │   │
│                       │   Executor   │   │
│                       └──────┬───────┘   │
└──────────────────────────────┼───────────┘
                               │
                               │ REST API
                               ▼
                    ┌─────────────────────┐
                    │   Broker APIs       │
                    │  - IG Markets       │
                    │  - OANDA           │
                    │  - IBKR            │
                    └─────────────────────┘
```

## Next Steps

For detailed information on specific architectural aspects, refer to the individual documentation files listed above.

For implementation guides, see the [Developer Documentation](../developer/).

For deployment information, see the [Operations Documentation](../operations/).