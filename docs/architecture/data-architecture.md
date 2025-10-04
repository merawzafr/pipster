# Data Architecture

## Overview

Pipster uses a multi-store data architecture with different storage systems optimized for specific data access patterns. The repository pattern abstracts data access, enabling migration between storage technologies without changing business logic.

---

## Storage Strategy

### Polyglot Persistence

Different data types use different storage technologies based on their characteristics.

```
┌─────────────────────────────────────────────────────┐
│                 Application Layer                    │
└─────────────────────┬───────────────────────────────┘
                      │
                      │ Repository Interfaces
                      │
         ┌────────────┼────────────┬──────────────┐
         │            │            │              │
         ▼            ▼            ▼              ▼
┌─────────────┐ ┌─────────┐ ┌──────────┐ ┌─────────────┐
│  Azure SQL  │ │  Redis  │ │   Blob   │ │  Cosmos DB  │
│             │ │         │ │ Storage  │ │  (Future)   │
│ - Tenants   │ │ - Cache │ │ - Session│ │ - Events    │
│ - Channels  │ │ - Idemp │ │   Files  │ │ - Audit     │
│ - Trading   │ │         │ │          │ │ - Analytics │
│ - Brokers   │ │         │ │          │ │             │
└─────────────┘ └─────────┘ └──────────┘ └─────────────┘
```

---

## Storage Systems

### 1. Relational Database (Azure SQL)

**Purpose**: Primary transactional data store

**Data Stored**:
- Tenants
- Channel configurations
- Trading configurations
- Broker connections

**Characteristics**:
- ACID transactions
- Strong consistency
- Relational integrity
- Query flexibility

**Current Implementation**: In-memory repositories (development)

**Production Implementation**: Azure SQL Database

**Schema Design**:

```
Tenants
├─ Id (PK, VARCHAR(50))
├─ Email (UNIQUE, VARCHAR(255))
├─ DisplayName (VARCHAR(255))
├─ Status (INT) - Active/Inactive/Suspended
├─ Plan (INT) - Free/Basic/Pro/Enterprise
├─ TelegramApiId (INT, NULLABLE)
├─ TelegramApiHash (VARCHAR(255), NULLABLE, ENCRYPTED)
├─ TelegramPhoneNumber (VARCHAR(50), NULLABLE)
├─ CreatedAt (DATETIMEOFFSET)
└─ DeactivatedAt (DATETIMEOFFSET, NULLABLE)

ChannelConfigurations
├─ Id (PK, VARCHAR(50))
├─ TenantId (FK → Tenants.Id)
├─ ChannelId (BIGINT)
├─ ChannelName (VARCHAR(255), NULLABLE)
├─ RegexPattern (NVARCHAR(MAX))
├─ IsEnabled (BIT)
├─ CreatedAt (DATETIMEOFFSET)
├─ UpdatedAt (DATETIMEOFFSET, NULLABLE)
└─ UNIQUE(TenantId, ChannelId)

TradingConfigurations
├─ Id (PK, VARCHAR(50))
├─ TenantId (FK → Tenants.Id, UNIQUE)
├─ SizingMode (INT) - Fixed/PercentEquity
├─ FixedUnits (INT, NULLABLE)
├─ EquityPercentage (DECIMAL(5,2), NULLABLE)
├─ MaxTotalExposurePercent (DECIMAL(5,2))
├─ MaxSlippagePips (DECIMAL(10,2), NULLABLE)
├─ WhitelistedSymbols (NVARCHAR(MAX)) - JSON array
├─ BlacklistedSymbols (NVARCHAR(MAX)) - JSON array
├─ TradingSessionStart (TIME, NULLABLE)
├─ TradingSessionEnd (TIME, NULLABLE)
├─ TradingSessionDays (VARCHAR(100), NULLABLE) - CSV
├─ AutoExecuteEnabled (BIT)
├─ CreatedAt (DATETIMEOFFSET)
└─ UpdatedAt (DATETIMEOFFSET, NULLABLE)

BrokerConnections
├─ Id (PK, VARCHAR(50))
├─ TenantId (FK → Tenants.Id)
├─ BrokerType (INT) - IGMarkets/OANDA/IBKR/etc.
├─ DisplayName (VARCHAR(255))
├─ IsActive (BIT)
├─ EncryptedCredentials (NVARCHAR(MAX))
├─ Metadata (NVARCHAR(MAX)) - JSON
├─ CreatedAt (DATETIMEOFFSET)
├─ UpdatedAt (DATETIMEOFFSET, NULLABLE)
└─ LastUsedAt (DATETIMEOFFSET, NULLABLE)

ChannelBrokerMappings (Many-to-Many)
├─ ChannelConfigurationId (FK → ChannelConfigurations.Id)
├─ BrokerConnectionId (FK → BrokerConnections.Id)
└─ PRIMARY KEY (ChannelConfigurationId, BrokerConnectionId)
```

**Indexes**:
```sql
-- Tenants
CREATE UNIQUE INDEX IX_Tenants_Email ON Tenants(Email);
CREATE INDEX IX_Tenants_Status ON Tenants(Status);

-- ChannelConfigurations
CREATE INDEX IX_Channels_TenantId ON ChannelConfigurations(TenantId);
CREATE UNIQUE INDEX IX_Channels_TenantChannel 
  ON ChannelConfigurations(TenantId, ChannelId);
CREATE INDEX IX_Channels_Enabled 
  ON ChannelConfigurations(TenantId, IsEnabled) 
  WHERE IsEnabled = 1;

-- BrokerConnections
CREATE INDEX IX_Brokers_TenantId ON BrokerConnections(TenantId);
CREATE INDEX IX_Brokers_TenantActive 
  ON BrokerConnections(TenantId, IsActive) 
  WHERE IsActive = 1;
CREATE INDEX IX_Brokers_TenantType 
  ON BrokerConnections(TenantId, BrokerType);
```

---

### 2. Cache (Redis)

**Purpose**: High-performance caching and distributed operations

**Data Stored**:
- Cached tenant configurations
- Cached channel configurations
- Cached trading configurations
- Idempotency keys

**Characteristics**:
- In-memory (sub-millisecond latency)
- Distributed (shared across workers)
- TTL-based eviction
- Atomic operations

**Key Patterns**:

```
Tenant Cache:
Key: pipster:tenant:{tenantId}
Value: JSON-serialized Tenant
TTL: 5 minutes

Channel Cache:
Key: pipster:channel:{tenantId}:{channelId}
Value: JSON-serialized ChannelConfiguration
TTL: 5 minutes

Trading Config Cache:
Key: pipster:trading:{tenantId}
Value: JSON-serialized TradingConfiguration
TTL: 5 minutes

Idempotency:
Key: pipster:idempotency:{tenantId}:{channelId}:{messageId}
Value: Timestamp
TTL: 24 hours
```

**Operations**:
- `GET`: Retrieve cached value
- `SET NX EX`: Set if not exists with expiration (idempotency)
- `DEL`: Invalidate cache
- `EXISTS`: Check key existence

**Cache Invalidation Strategy**:
- **Write-through**: Update cache when updating database
- **TTL-based**: Auto-expire after 5 minutes
- **Manual**: Invalidate on explicit updates

---

### 3. Blob Storage (Azure Blob)

**Purpose**: Large binary file storage

**Data Stored**:
- Telegram session files (td.binlog, db/, files/)

**Characteristics**:
- Unlimited capacity
- Low cost per GB
- Hierarchical structure
- Lifecycle management

**Container Structure**:

```
telegram-sessions/
├── {tenantId}/
│   ├── td.binlog           # Main session file
│   ├── db/
│   │   ├── td.db           # SQLite database
│   │   └── td.db-journal
│   └── files/
│       └── [media cache]
```

**Access Patterns**:
- **Write**: On session creation (one-time)
- **Read**: On worker startup (infrequent)
- **Update**: On session changes (rare)

**Lifecycle Management**:
```
Hot tier (frequent access):
  - Active tenants (< 30 days since last access)

Cool tier (infrequent access):
  - Inactive tenants (30-90 days)

Archive tier (rare access):
  - Deactivated tenants (> 90 days)
```

---

### 4. Event Store (Cosmos DB - Future)

**Purpose**: Append-only event log for audit and analytics

**Data Stored**:
- Signal received events
- Trade executed events
- Configuration change events
- User action events

**Characteristics**:
- Append-only (immutability)
- Time-series optimized
- Horizontal partitioning
- TTL-based retention

**Document Schema**:

```json
{
  "id": "evt_abc123",
  "type": "SignalReceived",
  "tenantId": "tenant-001",
  "timestamp": "2025-01-06T14:32:00Z",
  "data": {
    "channelId": 1234567890,
    "messageId": 9876543210,
    "symbol": "XAUUSD",
    "side": "Buy",
    "entry": 3750.5,
    "stopLoss": 3740.0,
    "takeProfits": [3753.0, 3760.0]
  },
  "metadata": {
    "correlationId": "abc123def456",
    "source": "TelegramWorker"
  },
  "ttl": 7776000  // 90 days in seconds
}
```

**Partition Key**: `tenantId` (isolates tenant data)

**Time-To-Live**: 90 days (compliance requirement)

---

## Repository Pattern

### Interface Definition

Repositories are defined in the **Domain Layer** and implemented in **Infrastructure Layer**.

**ITenantRepository**:
```
GetByIdAsync(id) → Tenant?
GetByEmailAsync(email) → Tenant?
GetActiveTenantsAsync() → List<Tenant>
AddAsync(tenant) → void
UpdateAsync(tenant) → void
DeleteAsync(id) → void
ExistsAsync(id) → bool
```

**IChannelConfigurationRepository**:
```
GetByIdAsync(id) → ChannelConfiguration?
GetByTenantAndChannelAsync(tenantId, channelId) → ChannelConfiguration?
GetByTenantIdAsync(tenantId) → List<ChannelConfiguration>
GetEnabledByTenantIdAsync(tenantId) → List<ChannelConfiguration>
AddAsync(config) → void
UpdateAsync(config) → void
DeleteAsync(id) → void
```

### Current Implementation: In-Memory

**Characteristics**:
- Thread-safe using ConcurrentDictionary
- Data lost on restart
- Fast (no I/O)
- Suitable for development/testing only

**Usage**:
```csharp
public class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();
    
    public Task<Tenant?> GetByIdAsync(string id, CancellationToken ct)
    {
        _tenants.TryGetValue(id, out var tenant);
        return Task.FromResult(tenant);
    }
}
```

### Future Implementation: Entity Framework Core

**Azure SQL Repository**:
```csharp
public class SqlTenantRepository : ITenantRepository
{
    private readonly PipsterDbContext _context;
    
    public async Task<Tenant?> GetByIdAsync(string id, CancellationToken ct)
    {
        return await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}
```

**DbContext**:
```csharp
public class PipsterDbContext : DbContext
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<ChannelConfiguration> ChannelConfigurations { get; set; }
    public DbSet<TradingConfiguration> TradingConfigurations { get; set; }
    public DbSet<BrokerConnection> BrokerConnections { get; set; }
}
```

---

## Data Access Patterns

### Read Patterns

**Hot Path (High Frequency)**:
```
Request → Cache (Redis)
             ├─ Hit → Return
             └─ Miss → Database → Cache → Return
```

**Cold Path (Low Frequency)**:
```
Request → Database → Return
```

**Cached Entities**:
- Tenant (read on every message)
- ChannelConfiguration (read on every message)
- TradingConfiguration (read on every message)

**Non-Cached Entities**:
- BrokerConnection (read on trade execution only)

### Write Patterns

**Write-Through Cache**:
```
Update Request
    │
    ├──► Update Database
    │
    ├──► Update Cache (or invalidate)
    │
    └──► Return Success
```

**Cache Invalidation**:
```
Update Tenant
    │
    └──► Invalidate: pipster:tenant:{tenantId}
         Invalidate: pipster:channel:{tenantId}:*
         Invalidate: pipster:trading:{tenantId}
```

---

## Data Consistency

### Strong Consistency

**Within Single Aggregate**:
- Tenant modifications are ACID transactions
- ChannelConfiguration updates are atomic
- TradingConfiguration changes are isolated

**Scope**: Single database transaction

### Eventual Consistency

**Cross-Aggregate Updates**:
- Tenant.AddChannel() + ChannelConfiguration.Create()
- Separate transactions
- Message bus ensures worker sees change

**Acceptable Delay**: < 5 minutes (cache TTL)

**Example Flow**:
```
1. API creates ChannelConfiguration (DB write)
2. API publishes AddChannelRequest (message bus)
3. Cache expires after 5 minutes
4. Worker receives message within seconds
5. Worker processes new channel
```

---

## Data Migration Strategy

### Phase 1: In-Memory (Current)

**Development/Testing Only**

**Pros**:
- Fast development
- No external dependencies
- Simple setup

**Cons**:
- No persistence
- Single instance only

---

### Phase 2: Azure SQL + Redis (Beta)

**Production Pilot**

**Migration Steps**:
1. Create Azure SQL database
2. Run EF Core migrations
3. Implement SqlTenantRepository
4. Update DI registration
5. Deploy with feature flag
6. Gradual rollout

**Backward Compatibility**:
- Keep in-memory for development
- SQL for production
- Same interface, different implementation

---

### Phase 3: Cosmos DB for Events (Scale)

**High-Volume Audit Trail**

**When to Migrate**:
- Event volume > 1M/day
- Azure SQL DTU exhausted
- Need global distribution

**Migration Strategy**:
- Add Cosmos DB in parallel
- Dual-write initially
- Validate data quality
- Cut over reads
- Deprecate SQL for events

---

## Data Security

### Encryption at Rest

**Database**:
- Azure SQL: Transparent Data Encryption (TDE) enabled
- Cosmos DB: Encryption enabled by default
- Blob Storage: Storage Service Encryption (SSE) enabled

**Redis**:
- Data encrypted in transit (TLS)
- Data encrypted at rest (Azure Cache for Redis Premium)

### Encryption in Transit

**All Connections**:
- TLS 1.2+ required
- Certificate validation enabled
- No plaintext protocols

### Sensitive Data

**Telegram Credentials**:
- Encrypted before storage
- Decrypted in-memory only
- Never logged

**Broker Credentials**:
- Encrypted before storage
- Decrypted on connector creation
- Future: Azure Key Vault integration

**Encryption Method** (Current):
- AES-256-GCM
- Key stored in Azure Key Vault
- Rotation: Every 90 days

---

## Backup & Recovery

### Database Backups

**Azure SQL**:
- Automated backups: Daily full + hourly differential
- Retention: 7 days (configurable up to 35 days)
- Point-in-time restore: Any point within retention
- Geo-redundant: Replicated to paired region

**Recovery Time Objective (RTO)**: < 1 hour

**Recovery Point Objective (RPO)**: < 1 hour

### Blob Storage Backups

**Telegram Sessions**:
- Soft delete enabled (14-day retention)
- Versioning enabled
- Geo-redundant storage (GRS)

**Recovery**: Restore from soft delete or previous version

### Redis (Cache)

**No Backups Required**:
- Cache can be rebuilt from database
- Idempotency keys expire naturally

**Recovery**: Cache warms up automatically

---

## Data Retention

### Operational Data

**Tenants**: Indefinite (soft delete)

**Configurations**: Indefinite (soft delete)

**Broker Connections**: Indefinite (until tenant deletes)

### Transactional Data

**Idempotency Keys**: 24 hours (Redis TTL)

**Events** (Future):
- Cosmos DB: 90 days (TTL)
- Cold storage: 7 years (compliance)

### Session Data

**Telegram Sessions**:
- Active tenants: Indefinite
- Deactivated tenants: 90 days → Archive tier

---

## Performance Optimization

### Database Query Optimization

**Best Practices**:
- Use indexes on foreign keys and frequently queried columns
- Avoid SELECT * (project only needed columns)
- Use AsNoTracking() for read-only queries
- Batch updates when possible
- Use compiled queries for hot paths

**Monitoring**:
- Query execution time
- DTU/CPU usage
- Index usage statistics
- Missing index recommendations

### Connection Pooling

**Configuration**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Min Pool Size=10;Max Pool Size=100;"
  }
}
```

**Min Pool Size**: 10 (pre-warm connections)

**Max Pool Size**: 100 (limit concurrent connections)

### Caching Effectiveness

**Metrics**:
- Cache hit rate (target: > 95%)
- Cache miss rate
- Average lookup time (target: < 10ms)

**Monitoring**:
```
Cache Hit Rate = Hits / (Hits + Misses) × 100%

Target: 95%+
Warning: < 90%
Critical: < 80%
```

---

## Data Governance

### Data Ownership

**Tenant Data**: Owned by tenant, isolated, tenant can request deletion

**System Data**: Owned by Pipster, used for operations

**Audit Data**: Owned by Pipster, retained for compliance

### GDPR Compliance

**Right to Access**: API endpoint to export tenant data

**Right to Deletion**: Soft delete with 30-day grace period

**Right to Portability**: Export in JSON format

**Data Minimization**: Only collect necessary data

### Audit Trail

**What to Audit** (Future):
- Tenant creation/deletion
- Configuration changes
- Signal received/processed
- Trade execution
- Admin actions

**Retention**: 90 days (Cosmos DB) + 7 years (Archive)

---

## Summary

Pipster's data architecture provides:

| Aspect | Strategy | Benefit |
|--------|----------|---------|
| Storage | Polyglot persistence | Right tool for each job |
| Access | Repository pattern | Technology independence |
| Performance | Multi-level caching | 95%+ cache hit rate |
| Consistency | Eventual where acceptable | Scalability without sacrificing correctness |
| Security | Encryption everywhere | Data protection |
| Backup | Automated with geo-redundancy | < 1 hour RTO/RPO |
| Scalability | Horizontal partitioning ready | Supports 10,000+ tenants |

The architecture balances consistency, performance, and scalability while maintaining flexibility for future growth.