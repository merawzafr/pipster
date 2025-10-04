# Domain Model

## Overview

The domain model represents the core business concepts in Pipster. It is designed using Domain-Driven Design (DDD) principles with clear aggregates, entities, and value objects.

---

## Entity Relationship Diagram

```
┌─────────────────────┐
│       Tenant        │
│─────────────────────│
│ + Id (PK)           │ 1
│ + Email (Unique)    ├──────────┐
│ + DisplayName       │          │
│ + Status            │          │
│ + Plan              │          │
│ + TelegramCreds     │          │ *
│ + ChannelIds[]      │    ┌─────▼──────────────────┐
└─────────────────────┘    │ ChannelConfiguration   │
                           │────────────────────────│
         │ 1               │ + Id (PK)              │
         │                 │ + TenantId (FK)        │
         │                 │ + ChannelId            │
         │                 │ + ChannelName          │
         │                 │ + RegexPattern         │
         │ *               │ + IsEnabled            │
┌────────▼──────────────┐  │ + BrokerConnectionIds[]│
│ TradingConfiguration  │  └────────────────────────┘
│───────────────────────│            │ *
│ + Id (PK)             │            │
│ + TenantId (FK)       │            │
│ + SizingMode          │            │
│ + FixedUnits          │            │
│ + EquityPercentage    │            │
│ + WhitelistedSymbols[]│            │
│ + BlacklistedSymbols[]│            │
│ + MaxExposure         │            │
│ + TradingSession      │            │
│ + AutoExecuteEnabled  │            │
└───────────────────────┘            │
                                     │
         │ *                         │
         │                           │
┌────────▼──────────────┐   ┌────────▼─────────────┐
│  BrokerConnection     │◄──│  (Referenced by)     │
│───────────────────────│   └──────────────────────┘
│ + Id (PK)             │
│ + TenantId (FK)       │
│ + BrokerType          │
│ + DisplayName         │
│ + IsActive            │
│ + EncryptedCreds      │
│ + Metadata            │
│ + LastUsedAt          │
└───────────────────────┘
```

---

## Aggregates

### 1. Tenant Aggregate

**Aggregate Root**: Tenant

**Bounded Context**: Tenant management and subscription

**Responsibilities**:
- Manage tenant lifecycle (create, activate, deactivate)
- Store Telegram authentication credentials
- Track subscribed channel IDs
- Enforce subscription plan rules

**Invariants**:
- Email must be unique across system
- Tenant must be Active to add/remove channels
- Telegram credentials required before monitoring channels

**State Transitions**:
```
     Created
        │
        │ SetTelegramCredentials()
        ▼
     Active ←─────┐
        │         │
        │         │ Reactivate()
        │         │
        │ Deactivate()
        │         │
        ▼         │
    Inactive ─────┘
        │
        │ Suspend() (payment issue, ToS violation)
        ▼
   Suspended
```

**Key Operations**:
- `Create()`: Factory method for new tenant
- `SetTelegramCredentials()`: Store API credentials
- `AddChannel()`: Add channel to monitoring list
- `RemoveChannel()`: Remove channel from monitoring
- `Deactivate()`: Soft delete tenant
- `ChangePlan()`: Update subscription tier

---

### 2. ChannelConfiguration (Independent Entity)

**Not part of Tenant aggregate** - designed for scalability and independent lifecycle.

**Bounded Context**: Telegram channel monitoring

**Responsibilities**:
- Store regex pattern for signal parsing
- Manage enabled/disabled state
- Route signals to specific brokers (optional)

**Invariants**:
- Regex pattern must be syntactically valid
- Enabled channels must have valid regex
- TenantId must reference existing tenant

**Key Operations**:
- `Create()`: Factory method with regex validation
- `UpdateRegexPattern()`: Change parsing pattern
- `Enable()/Disable()`: Toggle signal processing
- `AddBrokerConnection()`: Route to specific broker
- `SetBrokerConnections()`: Replace broker routing

---

### 3. TradingConfiguration (Per-Tenant Singleton)

**Bounded Context**: Risk management and trading rules

**Responsibilities**:
- Define position sizing strategy
- Manage symbol whitelists/blacklists
- Set trading session hours
- Control auto-execution behavior

**Invariants**:
- Symbol cannot be in both whitelist and blacklist
- If whitelist is empty, all symbols allowed (except blacklisted)
- Position sizing must be positive
- Max exposure must be 0.01-100%
- Auto-execution defaults to false (safe default)

**Key Operations**:
- `CreateDefault()`: Factory method with safe defaults
- `SetFixedSizing()`: Configure fixed units per trade
- `SetPercentageSizing()`: Configure equity-based sizing
- `WhitelistSymbol()`: Allow specific symbol
- `BlacklistSymbol()`: Forbid specific symbol
- `SetTradingSession()`: Define UTC trading hours
- `EnableAutoExecution()`: Allow automatic trading

---

### 4. BrokerConnection (Independent Entity)

**Bounded Context**: Broker integration management

**Responsibilities**:
- Store encrypted broker credentials
- Manage active/inactive state
- Track last usage timestamp
- Store broker-specific metadata

**Invariants**:
- TenantId must reference existing tenant
- Credentials must be encrypted before storage
- DisplayName required for user identification

**Key Operations**:
- `Create()`: Factory method for new connection
- `UpdateCredentials()`: Change API keys
- `Activate()/Deactivate()`: Toggle availability
- `MarkAsUsed()`: Update last used timestamp
- `SetMetadata()`: Store broker-specific settings

---

## Value Objects

### TradingSession

**Purpose**: Define when trading is allowed (UTC times)

**Properties**:
- `StartUtc`: Session start time (TimeOnly)
- `EndUtc`: Session end time (TimeOnly)
- `AllowedDays`: Optional day-of-week filter

**Behavior**:
- `IsWithinSession()`: Check if current time is within session
- Handles sessions that cross midnight (e.g., 22:00 - 02:00)

**Immutability**: Record type, all properties init-only

---

### TelegramCredentials

**Purpose**: Store Telegram API authentication

**Properties**:
- `ApiId`: Telegram API ID (from my.telegram.org)
- `ApiHash`: Telegram API hash
- `PhoneNumber`: Optional, for display only

**Storage**: Encrypted in database, decrypted in memory only

**Immutability**: Record type, all properties init-only

---

## Enumerations

### TenantStatus
```
- Active (1): Fully operational
- Inactive (2): Deactivated by user or admin
- Suspended (3): Suspended due to policy violation or payment issue
```

### SubscriptionPlan
```
- Free (0): Limited features
- Basic (1): Standard features
- Pro (2): Advanced features
- Enterprise (3): Custom limits and SLA
```

### BrokerType
```
- IGMarkets (1): IG Markets (UK-based)
- OANDA (2): OANDA (US/Global)
- IBKR (3): Interactive Brokers
- Alpaca (4): Alpaca (US stocks)
- ForexCom (5): FOREX.com
```

### PositionSizingMode
```
- Fixed (1): Fixed units per trade
- PercentEquity (2): Percentage of account equity
```

### OrderSide
```
- Buy: Long position
- Sell: Short position
```

---

## Business Rules

### Tenant Rules

**R1: Email Uniqueness**
- Each email can only be used once across the system
- Enforced at: Tenant creation
- Check: ITenantRepository.GetByEmailAsync()

**R2: Active Status for Operations**
- Only Active tenants can add/remove channels
- Enforced at: Tenant.AddChannel(), Tenant.RemoveChannel()
- Throws: InvalidOperationException if not Active

**R3: Telegram Credentials Required**
- Tenant must have Telegram credentials before monitoring channels
- Enforced at: Application layer (TenantService)
- Recommended: Set credentials immediately after creation

---

### Channel Configuration Rules

**R4: Regex Pattern Validity**
- All regex patterns must compile without errors
- Enforced at: ChannelConfiguration.Create(), UpdateRegexPattern()
- Validation: System.Text.RegularExpressions.Regex.Match()

**R5: Enabled Channels Have Valid Regex**
- Channel can only be enabled if regex is valid
- Enforced at: ChannelConfiguration.Enable()
- Prevents: Runtime parsing failures

---

### Trading Configuration Rules

**R6: Symbol Exclusivity**
- Symbol cannot be in both whitelist and blacklist
- Enforced at: WhitelistSymbol(), BlacklistSymbol()
- Behavior: Adding to one removes from the other

**R7: Whitelist Precedence**
- If whitelist is empty, all symbols allowed (except blacklisted)
- If whitelist has symbols, only those are allowed
- Blacklist always takes precedence over whitelist
- Enforced at: TradingConfiguration.IsSymbolAllowed()

**R8: Position Sizing Constraints**
- Fixed units must be > 0
- Equity percentage must be 0.01 - 100%
- Enforced at: SetFixedSizing(), SetPercentageSizing()
- Throws: ArgumentException if invalid

**R9: Max Exposure Limits**
- Must be between 0.01 and 100%
- Enforced at: SetMaxExposure()
- Purpose: Prevent over-leveraging

**R10: Auto-Execution Default**
- Defaults to false (disabled) for safety
- Must be explicitly enabled by user
- Enforced at: CreateDefault()

---

### Broker Connection Rules

**R11: Credential Encryption**
- Credentials must be encrypted before storage
- Never stored in plaintext
- Enforced at: Application layer before calling repository
- Future: Azure Key Vault integration

**R12: Active State for Trading**
- Only active connections can execute trades
- Enforced at: TradeConnectorFactory.GetConnectorAsync()
- Throws: InvalidOperationException if inactive

---

## Domain Events (Future)

Currently using message bus for events. Future domain events:

**TenantEvents**
- TenantCreated
- TenantActivated
- TenantDeactivated
- TelegramCredentialsSet

**ChannelEvents**
- ChannelAdded
- ChannelRemoved
- ChannelEnabled
- ChannelDisabled

**TradingConfigEvents**
- AutoExecutionEnabled
- AutoExecutionDisabled
- SymbolWhitelisted
- SymbolBlacklisted

**BrokerEvents**
- BrokerConnectionCreated
- BrokerConnectionActivated
- BrokerConnectionDeactivated

---

## Repository Interfaces

### ITenantRepository
```
GetByIdAsync(id)
GetByEmailAsync(email)
GetActiveTenantsAsync()
GetByStatusAsync(status)
AddAsync(tenant)
UpdateAsync(tenant)
DeleteAsync(id)
ExistsAsync(id)
```

### IChannelConfigurationRepository
```
GetByIdAsync(id)
GetByTenantAndChannelAsync(tenantId, channelId)
GetByTenantIdAsync(tenantId)
GetEnabledByTenantIdAsync(tenantId)
AddAsync(config)
UpdateAsync(config)
DeleteAsync(id)
ExistsAsync(tenantId, channelId)
```

### ITradingConfigurationRepository
```
GetByIdAsync(id)
GetByTenantIdAsync(tenantId)
AddAsync(config)
UpdateAsync(config)
DeleteAsync(id)
ExistsForTenantAsync(tenantId)
```

### IBrokerConnectionRepository
```
GetByIdAsync(id)
GetByTenantIdAsync(tenantId)
GetActiveByTenantIdAsync(tenantId)
GetByTenantAndTypeAsync(tenantId, brokerType)
AddAsync(connection)
UpdateAsync(connection)
DeleteAsync(id)
ExistsAsync(id)
```

---

## Aggregate Consistency Boundaries

### Strong Consistency

**Within Tenant Aggregate**:
- Adding/removing channels must immediately reflect in SubscribedChannelIds
- State changes (Active/Inactive) must be atomic
- Transaction boundary: Single tenant

**Within TradingConfiguration**:
- Whitelist/blacklist changes must be atomic
- Moving symbol between lists must be transactional
- Transaction boundary: Single configuration

### Eventual Consistency

**Between Tenant and ChannelConfiguration**:
- Tenant.AddChannel() updates tenant immediately
- ChannelConfiguration created in separate transaction
- Message bus ensures TelegramWorker eventually sees new channel

**Between ChannelConfiguration and BrokerConnection**:
- Channel can reference broker connections that don't exist yet
- Application layer validates references exist
- No FK constraint at database level (planned feature)

---

## Domain Model Evolution

### Current Version (v1.0)
- Basic multi-tenancy
- Channel-based signal routing
- Simple risk rules
- Single broker connection per channel or tenant default

### Planned (v2.0)
- Multi-account support (multiple accounts per broker)
- Advanced position sizing (Kelly Criterion, risk-based)
- Signal scoring and filtering
- Broker failover (primary/backup)
- Trade copying (mirror trades across accounts)

### Under Consideration (v3.0)
- Portfolio-level risk management
- Cross-tenant signal sharing
- Advanced analytics and backtesting
- ML-based signal validation

---

## Summary

The domain model is designed around these key principles:

1. **Clear Aggregates**: Tenant is the main aggregate; other entities are independent for scalability
2. **Business Rule Enforcement**: All rules enforced in domain entities, not services
3. **Immutable Value Objects**: Trading sessions and credentials are immutable
4. **Factory Methods**: Entities created via static factory methods with validation
5. **Rich Domain Model**: Entities contain behavior, not just data
6. **Repository Pattern**: Data access abstracted behind interfaces
7. **No External Dependencies**: Domain layer has zero framework dependencies

This design ensures the business logic remains pure, testable, and independent of infrastructure concerns.