# Layer Architecture

## Overview

Pipster follows **Clean Architecture** principles with strict dependency rules. Each layer has specific responsibilities and dependencies flow inward toward the domain.

## Dependency Rule

```
┌─────────────────────────────────────────┐
│         Presentation Layer              │
│    (API, Workers, Host)                 │
└──────────────┬──────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────┐
│       Application Layer                 │
│    (Services, Handlers, Parsers)        │
└──────────────┬──────────────────────────┘
               │ depends on
               ▼
┌──────────────────────────────────────┬──┐
│       Domain Layer                   │  │
│    (Entities, Interfaces)            │  │
└──────────────────────────────────────┘  │
               ▲                           │
               │ implements                │
               │                           │
┌──────────────┴───────────────────────┐  │
│    Infrastructure Layer              │◄─┘
│  (Repositories, Telegram, Messaging) │
└──────────────────────────────────────┘
```

**Key Principle**: Dependencies point inward. The domain never depends on outer layers.

---

## Domain Layer (`Pipster.Domain`)

### Responsibility
Contains core business logic and rules. This is the heart of the application.

### Contents
- **Entities**: Core business objects with identity and lifecycle
- **Value Objects**: Immutable objects defined by their attributes
- **Repository Interfaces**: Contracts for data access (implementations in Infrastructure)
- **Enums**: Domain-specific enumerations

### Dependencies
**NONE** - The domain has zero external dependencies. This ensures business logic remains pure and testable.

### Key Entities

**Tenant**
- Aggregate root for tenant-related operations
- Manages channel subscriptions
- Enforces business rules (active status required for operations)

**ChannelConfiguration**
- Configuration for monitoring a Telegram channel
- Contains regex pattern for signal parsing
- Manages broker routing preferences

**TradingConfiguration**
- Risk management rules per tenant
- Symbol whitelisting/blacklisting
- Position sizing strategies
- Trading session constraints

**BrokerConnection**
- Represents a connection to a broker
- Stores encrypted credentials
- Manages active/inactive state

### Invariants Enforced
- Tenants cannot add channels unless status is Active
- Symbols cannot be in both whitelist and blacklist
- Regex patterns must be valid before saving
- Position sizing must be greater than zero

---

## Application Layer (`Pipster.Application`)

### Responsibility
Orchestrates use cases by coordinating domain entities and infrastructure services.

### Contents
- **Services**: High-level business operations
  - TenantService: Tenant CRUD operations
  - ChannelManagementService: Channel configuration
  - TradingConfigService: Risk settings management
  
- **Handlers**: Message processing pipelines
  - TelegramMessageHandlerWorker: Processes incoming Telegram messages
  
- **Parsers**: Signal extraction logic
  - RegexSignalParser: Extracts trading signals from text
  
- **Providers**: Cached data access
  - CachedTenantConfigProvider: Reduces database load

### Dependencies
- **Domain Layer**: Uses entities and repository interfaces
- **Infrastructure Layer**: Uses implementations via dependency injection

### Responsibilities

**TenantService**
- Creates tenants with validation
- Updates Telegram credentials
- Manages tenant lifecycle (activate/deactivate)
- Changes subscription plans

**ChannelManagementService**
- Adds/removes channels
- Updates regex patterns
- Enables/disables channels
- Publishes channel requests to message bus

**TradingConfigService**
- Configures position sizing
- Manages symbol whitelists/blacklists
- Sets trading session hours
- Enables/disables auto-execution

**TelegramMessageHandlerWorker**
- Implements complete message processing pipeline
- Coordinates: idempotency → validation → parsing → risk → execution
- Publishes trade commands to executor

### Pattern: Application Services

Application services follow this pattern:
1. Validate inputs
2. Load domain entities from repositories
3. Execute domain logic
4. Persist changes
5. Publish events/messages

They orchestrate but do not contain business logic (that lives in entities).

---

## Infrastructure Layer (`Pipster.Infrastructure`)

### Responsibility
Implements interfaces defined by inner layers. Handles all external concerns.

### Contents

**Repositories**
- InMemoryTenantRepository (development)
- InMemoryChannelConfigurationRepository
- InMemoryTradingConfigurationRepository
- Future: SqlTenantRepository, CosmosTenantRepository

**Messaging**
- InMemoryBus: Channel-based message queue
- Future: AzureServiceBusAdapter

**Telegram**
- ResilientTelegramClient: TDLib wrapper with resilience
- TelegramClientManager: Multi-tenant connection pooling
- LocalFileSessionStore: Development session storage
- AzureBlobSessionStore: Production session storage

**Idempotency**
- InMemoryIdempotencyStore: Development deduplication
- RedisIdempotencyStore: Production distributed deduplication

**Connectors**
- TradeConnectorFactory: Creates and manages broker connectors
- Future: Credential decryption using Azure Key Vault

### Dependencies
- **Domain Layer**: Implements repository interfaces
- **External Libraries**: TDLib, Azure SDK, StackExchange.Redis, Polly

### Key Implementations

**InMemoryRepositories**
- Thread-safe using ConcurrentDictionary
- Data lost on restart (development only)
- Fast and simple for testing

**TelegramClientManager**
- Pools client instances (one per tenant)
- Implements health checks
- Handles graceful shutdown
- Maximum clients configurable (default: 1000)

**AzureBlobSessionStore**
- Stores session files in Azure Blob Storage
- Structure: `/{tenantId}/td.binlog`, `/db/`, `/files/`
- Syncs local cache with blob on startup
- Enables session recovery without re-authentication

**RedisIdempotencyStore**
- Uses SET NX for atomic operations
- 24-hour TTL for message keys
- Distributed deduplication across workers

---

## Presentation Layer

### API (`Pipster.Api`)

**Responsibility**: HTTP endpoints for external clients

**Contents**
- Controllers: TenantsController, StateController
- DTOs: Request/response models
- Swagger: OpenAPI documentation

**Dependencies**
- Application Layer: Calls application services
- ASP.NET Core framework

**Endpoints**
- `POST /api/tenants`: Create tenant
- `GET /api/tenants/{id}`: Get tenant details
- `POST /api/tenants/{id}/channels`: Add channel
- `PATCH /api/tenants/{id}/trading-config`: Update trading config

### Workers

**Pipster.Workers.Telegram**
- Background service managing Telegram connections
- Consumes AddChannelRequest and RemoveChannelRequest
- Runs periodic health checks
- Publishes TelegramMessageReceived events

**Pipster.Workers.Executor**
- Background service executing trades
- Consumes TradeCommand messages
- Uses connector factory to get appropriate broker
- Logs execution results

**Dependencies**
- Application Layer: Message handlers
- Infrastructure Layer: Message bus, connectors

### Host (`Pipster.Host`)

**Responsibility**: Composition root - wires everything together

**Contents**
- Program.cs: DI registration and startup

**Pattern: Composition Root**
This is the ONLY place that knows about all implementations. It:
1. Registers infrastructure services
2. Registers application services
3. Registers worker services
4. Registers broker connectors
5. Configures options from configuration

No other layer knows about specific implementations.

---

## Connector Layer (`Pipster.Connectors.*`)

### Responsibility
Broker-specific integrations implementing standard interface

### Pattern
Each connector is a separate project:
- `Pipster.Connectors.IGMarkets`
- `Pipster.Connectors.FXCM`
- Future: `Pipster.Connectors.OANDA`, `Pipster.Connectors.IBKR`

### Structure
```
Pipster.Connectors.IGMarkets/
├── IGMarketsConnector.cs           # ITradeConnector implementation
├── IGMarketsConnectorProvider.cs   # ITradeConnectorProvider implementation
├── IGMarketsApiClient.cs           # HTTP client
├── IGSessionManager.cs             # Session lifecycle
├── IGSymbolMapper.cs               # Symbol conversion
├── Configuration/                  # Options and DI
└── Models/                         # DTOs
```

### Dependencies
- **Shared Layer**: ITradeConnector interface
- **External**: Broker-specific SDK or HTTP client
- **Infrastructure**: Polly for resilience

### Registration
Connectors register themselves in Host via extension methods:
```csharp
builder.Services.AddIGMarketsConnector(configuration);
```

Workers remain completely unaware of specific brokers.

---

## Shared Layer (`Pipster.Shared`)

### Responsibility
Cross-cutting contracts used by multiple layers

### Contents
- **Contracts**: DTOs and interfaces
  - NormalizedSignal
  - TradeCommand
  - TelegramMessageReceived
  - ITradeConnector interface
  
- **Enums**: Shared enumerations
  - OrderSide (Buy/Sell)

### Dependencies
**NONE** - Shared by all layers

### Usage
- Domain and Application define their own specific models
- Shared contains only truly cross-cutting contracts
- Avoids circular dependencies between layers

---

## Layer Communication Patterns

### Inward Communication (Normal)
Outer layers call inner layers directly via dependency injection.

Example: Controller → Service → Repository
```
TenantsController
    └─> TenantService.CreateTenantAsync()
        └─> ITenantRepository.AddAsync()
            └─> InMemoryTenantRepository.AddAsync()
```

### Outward Communication (Events)
Inner layers communicate outward via events/messages (no direct dependency).

Example: Service publishes event → Worker handles it
```
ChannelManagementService
    └─> _messageBus.PublishChannelRequestAsync()
        
TelegramWorkerService
    └─> _messageBus.ConsumeChannelRequestsAsync()
```

This maintains the dependency rule while allowing inner layers to trigger actions in outer layers.

---

## Benefits of This Architecture

### Testability
- Domain logic tests with zero dependencies
- Application services mock repositories
- Infrastructure tests can run in isolation

### Maintainability
- Clear separation of concerns
- Changes isolated to specific layers
- Business rules centralized in domain

### Flexibility
- Swap implementations without changing domain
- Example: InMemoryBus → AzureServiceBus
- Example: InMemoryRepository → SqlRepository

### Independence
- Domain doesn't care about database type
- Domain doesn't care about messaging infrastructure
- Domain doesn't care about broker implementations

### Scalability
- Horizontal scaling by adding worker instances
- Vertical scaling possible per layer
- Caching strategies isolated to infrastructure

---

## Anti-Patterns to Avoid

### ❌ Domain Depending on Infrastructure
Never reference Infrastructure from Domain.

**Wrong**:
```csharp
// In Domain layer
public class Tenant
{
    public async Task SaveToDatabase() // ❌ Domain knows about persistence
    {
        await _dbContext.SaveChangesAsync();
    }
}
```

**Correct**:
```csharp
// In Domain layer
public class Tenant
{
    // Pure business logic only
}

// In Application layer
public class TenantService
{
    public async Task CreateTenantAsync(...)
    {
        var tenant = Tenant.Create(...);
        await _repository.AddAsync(tenant); // Repository handles persistence
    }
}
```

### ❌ Infrastructure Containing Business Logic
Business rules belong in Domain, not Infrastructure.

**Wrong**:
```csharp
// In Infrastructure
public class SqlTenantRepository
{
    public async Task AddAsync(Tenant tenant)
    {
        if (tenant.Email.Contains("@gmail")) // ❌ Business rule in repository
            throw new Exception("Gmail not allowed");
        
        await _dbContext.Tenants.AddAsync(tenant);
    }
}
```

**Correct**:
```csharp
// In Domain
public class Tenant
{
    public static Tenant Create(string email, ...)
    {
        if (email.Contains("@gmail"))
            throw new ArgumentException("Gmail not allowed");
        
        return new Tenant { Email = email };
    }
}

// In Infrastructure
public class SqlTenantRepository
{
    public async Task AddAsync(Tenant tenant)
    {
        await _dbContext.Tenants.AddAsync(tenant); // Just persistence
    }
}
```

### ❌ Application Services Containing Domain Logic
Keep domain logic in entities, not services.

**Wrong**:
```csharp
// In Application
public class TenantService
{
    public async Task AddChannelAsync(string tenantId, string channelId)
    {
        var tenant = await _repository.GetByIdAsync(tenantId);
        
        // ❌ Business logic in service
        if (tenant.Status != TenantStatus.Active)
            throw new Exception("Tenant not active");
        
        tenant.SubscribedChannelIds.Add(channelId);
        await _repository.UpdateAsync(tenant);
    }
}
```

**Correct**:
```csharp
// In Domain
public class Tenant
{
    public void AddChannel(string channelId)
    {
        // Business logic in entity
        if (Status != TenantStatus.Active)
            throw new InvalidOperationException("Tenant not active");
        
        _subscribedChannelIds.Add(channelId);
    }
}

// In Application
public class TenantService
{
    public async Task AddChannelAsync(string tenantId, string channelId)
    {
        var tenant = await _repository.GetByIdAsync(tenantId);
        tenant.AddChannel(channelId); // Delegate to domain
        await _repository.UpdateAsync(tenant);
    }
}
```

---

## Summary

| Layer | Responsibility | Dependencies | Examples |
|-------|---------------|--------------|----------|
| Domain | Business logic and rules | None | Tenant, ChannelConfiguration |
| Application | Orchestrate use cases | Domain | TenantService, SignalParser |
| Infrastructure | External integrations | Domain, External libs | Repositories, TelegramClient |
| Presentation | User/system interface | Application | API Controllers, Workers |
| Shared | Cross-cutting contracts | None | ITradeConnector, TradeCommand |
| Connectors | Broker integrations | Shared, External libs | IGMarketsConnector |

This layered architecture ensures maintainability, testability, and flexibility as the system evolves.