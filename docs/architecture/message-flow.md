# Message Flow Architecture

## Overview

Pipster uses an event-driven architecture where components communicate via messages through a central message bus. This enables loose coupling, horizontal scalability, and asynchronous processing.

---

## Message Bus Architecture

### Current Implementation: InMemoryBus

```
┌──────────────────────────────────────────────┐
│           InMemoryBus                        │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Channel<TelegramMessageReceived>    │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Channel<TradeCommand>               │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Channel<AddChannelRequest>          │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Channel<RemoveChannelRequest>       │   │
│  └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

**Characteristics**:
- Unbounded channels (no backpressure)
- Single process only
- No persistence
- Fast and simple for development

### Future Implementation: Azure Service Bus

```
┌──────────────────────────────────────────────┐
│         Azure Service Bus                    │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Topic: telegram-messages            │   │
│  │    └─ Subscription: handler          │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Queue: trade-commands               │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Queue: channel-requests             │   │
│  └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

**Benefits**:
- Distributed processing
- Message persistence
- Dead letter queues
- Scalable to thousands of workers

---

## End-to-End Message Flow

### Complete Pipeline: Telegram Signal → Broker Order

```
┌─────────────────────────────────────────────────────────┐
│ 1. Telegram Message Arrives                             │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ TelegramWorkerService                                   │
│ - Receives via TDLib UpdateNewMessage                   │
│ - Filters by observed channels                          │
│ - Extracts text content                                 │
│ - Publishes: TelegramMessageReceived                    │
└─────────────┬───────────────────────────────────────────┘
              │
              │ Message Bus
              ▼
┌─────────────────────────────────────────────────────────┐
│ 2. TelegramMessageHandlerWorker Consumes               │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 1: Idempotency Check                     │
│ - Key: {tenantId}:{channelId}:{messageId}              │
│ - Redis SET NX (atomic)                                 │
│ - If duplicate → Skip entire pipeline                   │
│ - TTL: 24 hours                                         │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 2: Tenant Validation                     │
│ - Lookup tenant (cached, 5min TTL)                      │
│ - Check Status == Active                                │
│ - If not active → Skip                                  │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 3: Channel Config Lookup                 │
│ - Lookup channel config (cached)                        │
│ - Check IsEnabled == true                               │
│ - Get regex pattern                                     │
│ - If disabled → Skip                                    │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 4: Signal Parsing                        │
│ - Apply channel-specific regex                          │
│ - Extract: symbol, side, entry, SL, TPs                 │
│ - Create NormalizedSignal                               │
│ - If no match → Skip                                    │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 5: Trading Config Lookup                 │
│ - Lookup trading config (cached)                        │
│ - Get risk rules                                        │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 6: Risk Validation                       │
│ - Check symbol allowed (whitelist/blacklist)            │
│ - Check within trading session                          │
│ - Check auto-execute enabled                            │
│ - If rejected → Log and skip                            │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 7: Determine Brokers                     │
│ - Use channel-specific brokers OR                       │
│ - Use tenant default brokers                            │
│ - Validate at least one active broker                   │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Pipeline Step 8: Create Trade Commands                 │
│ - One TradeCommand per broker                           │
│ - Calculate position size (fixed or % equity)           │
│ - Select TP1 (first take profit)                        │
│ - Publish: TradeCommand                                 │
└─────────────┬───────────────────────────────────────────┘
              │
              │ Message Bus
              ▼
┌─────────────────────────────────────────────────────────┐
│ 3. TradeExecutor Consumes                              │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Executor Step 1: Get Connector                         │
│ - Factory.GetConnectorAsync(brokerConnectionId)         │
│ - Loads broker connection from database                 │
│ - Decrypts credentials                                  │
│ - Creates or retrieves cached connector                 │
│ - Validates connection                                  │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Executor Step 2: Place Order                           │
│ - Convert symbol to broker format                       │
│ - Build broker-specific request                         │
│ - Send HTTP/API request                                 │
│ - Wait for confirmation (with timeout)                  │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│ Executor Step 3: Log Result                            │
│ - Success: Log order ID                                 │
│ - Failure: Log error, don't crash worker                │
│ - Future: Publish OrderExecuted event                   │
└─────────────────────────────────────────────────────────┘
```

**Total Latency Target**: p95 < 1.5 seconds (Telegram → Broker)

**Breakdown**:
- Telegram → Worker: < 100ms
- Worker → Bus: < 10ms
- Bus → Handler: < 50ms
- Handler processing: < 300ms
- Handler → Executor: < 50ms
- Executor → Broker API: < 1000ms

---

## Message Contracts

### TelegramMessageReceived

**Publisher**: TelegramWorkerService  
**Consumer**: TelegramMessageHandlerWorker

**Purpose**: Notify that a new message arrived from a monitored channel

**Schema**:
```
TenantId: string (required)
ChannelId: long (required)
MessageId: long (required)
Content: string (required)
Timestamp: DateTimeOffset (required)
SenderId: long (required)
MessageKey: string (computed property)
```

**MessageKey Format**: `{TenantId}:{ChannelId}:{MessageId}`

**Example**:
```json
{
  "tenantId": "tenant-001",
  "channelId": 1234567890,
  "messageId": 9876543210,
  "content": "Buy #XAUUSD 3750.5\nSL 3740\nTP 3753\nTP 3760",
  "timestamp": "2025-01-06T14:32:00Z",
  "senderId": 1111111111
}
```

---

### TradeCommand

**Publisher**: TelegramMessageHandlerWorker  
**Consumer**: TradeExecutor

**Purpose**: Instruct executor to place a trade at a specific broker

**Schema**:
```
TenantId: string (required)
Symbol: string (required)
Side: OrderSide (required) - Buy or Sell
Units: int (required)
Price: decimal? (nullable) - null = market order
StopLoss: decimal? (nullable)
TakeProfit: decimal? (nullable)
CorrelationId: string (required) - signal hash for tracking
CreatedAt: DateTimeOffset (required)
BrokerConnectionId: string (required) - which broker to use
SourceChannelId: string? (optional) - for audit trail
```

**Example**:
```json
{
  "tenantId": "tenant-001",
  "symbol": "XAUUSD",
  "side": "Buy",
  "units": 1000,
  "price": null,
  "stopLoss": 3740.0,
  "takeProfit": 3753.0,
  "correlationId": "abc123def456",
  "createdAt": "2025-01-06T14:32:01Z",
  "brokerConnectionId": "broker-001",
  "sourceChannelId": "1234567890"
}
```

---

### AddChannelRequest

**Publisher**: ChannelManagementService (via API)  
**Consumer**: TelegramWorkerService

**Purpose**: Request to start monitoring a Telegram channel

**Schema**:
```
TenantId: string (required)
ChannelId: long (required)
ChannelName: string? (optional) - for display only
```

**Flow**:
1. API receives POST /api/tenants/{id}/channels
2. ChannelManagementService creates ChannelConfiguration in database
3. Service publishes AddChannelRequest to bus
4. TelegramWorkerService consumes message
5. Worker gets/creates TelegramClient for tenant
6. Worker adds channel to client's observation list
7. Client joins Telegram channel
8. Future messages from channel will be published

---

### RemoveChannelRequest

**Publisher**: ChannelManagementService (via API)  
**Consumer**: TelegramWorkerService

**Purpose**: Request to stop monitoring a Telegram channel

**Schema**:
```
TenantId: string (required)
ChannelId: long (required)
```

**Flow**:
1. API receives DELETE /api/tenants/{id}/channels/{channelId}
2. ChannelManagementService deletes ChannelConfiguration from database
3. Service publishes RemoveChannelRequest to bus
4. TelegramWorkerService consumes message
5. Worker gets client for tenant (if exists)
6. Worker removes channel from observation list
7. Client leaves Telegram channel (optional)

---

## Message Processing Patterns

### Publish-Subscribe Pattern

Used for TelegramMessageReceived (future: multiple handlers)

```
Publisher: TelegramWorkerService
    │
    │ Publish
    ▼
Message Bus (Topic)
    │
    ├──► Handler 1: TelegramMessageHandlerWorker
    ├──► Handler 2: AuditLogger (future)
    └──► Handler 3: Analytics (future)
```

**Benefits**:
- Multiple consumers without publisher knowing
- Easy to add new handlers
- Each handler processes independently

### Point-to-Point Pattern

Used for TradeCommand (single executor per message)

```
Publisher: TelegramMessageHandlerWorker
    │
    │ Publish
    ▼
Message Bus (Queue)
    │
    └──► Single Consumer: TradeExecutor
         (or competing consumers for scale)
```

**Benefits**:
- Exactly-once processing
- Load balancing across multiple executors
- Guaranteed delivery

---

## Error Handling Strategies

### Retry with Backoff

**Applied to**: HTTP calls, transient failures

**Pattern**:
- Attempt 1: Immediate
- Attempt 2: Wait 2 seconds
- Attempt 3: Wait 4 seconds
- Attempt 4: Wait 8 seconds
- After max retries: Move to dead letter queue

**Implementation**: Polly retry policies

---

### Dead Letter Queue (DLQ)

**Purpose**: Capture messages that fail after max retries

**Future Implementation**:
```
┌──────────────────┐
│  Main Queue      │
│  (trade-commands)│
└────────┬─────────┘
         │
         │ Fails 3 times
         ▼
┌──────────────────┐
│  Dead Letter     │
│  Queue (DLQ)     │
└────────┬─────────┘
         │
         ▼
   Manual Review
   or
   Automated Retry
```

**Benefits**:
- No message loss
- Troubleshooting capability
- Automatic retry with fixes

---

### Idempotency

**Purpose**: Prevent duplicate processing of same message

**Implementation**:
- Redis SET NX (atomic check-and-set)
- Key: `pipster:idempotency:{tenantId}:{channelId}:{messageId}`
- TTL: 24 hours
- Returns: true if first time, false if duplicate

**Why Needed**:
- Message bus may deliver same message twice
- Network retries can cause duplicates
- Prevents placing same trade multiple times

**Where Applied**:
- TelegramMessageHandlerWorker (first step in pipeline)
- Future: TradeExecutor (if message bus doesn't guarantee once)

---

## Message Routing Strategies

### Channel-Specific Broker Routing

Channel configuration can specify which brokers to use:

```
ChannelConfiguration
  ├─ ChannelId: 123456
  ├─ RegexPattern: "..."
  └─ BrokerConnectionIds: ["broker-ig-001", "broker-oanda-002"]
```

**Flow**:
1. Signal parsed from channel 123456
2. Handler checks channel.BrokerConnectionIds
3. If not empty: Use those specific brokers
4. If empty: Use tenant's default active brokers
5. Create one TradeCommand per broker

**Use Cases**:
- Different brokers for different signal sources
- A/B testing broker execution quality
- Broker specialization (one for metals, one for forex)

---

### Tenant Default Broker Routing

If channel doesn't specify brokers, use all active brokers for tenant:

**Flow**:
1. Load all active BrokerConnections for tenant
2. Filter: IsActive == true
3. Create one TradeCommand per active broker

**Use Cases**:
- Simple setup for new users
- Replicate trades across multiple accounts
- Broker redundancy

---

## Message Bus Migration Path

### Phase 1: InMemoryBus (Current)

**Characteristics**:
- Single process
- No persistence
- Fast development
- Data loss on restart

**Sufficient for**: Development, testing, MVP

---

### Phase 2: Azure Service Bus

**Migration Steps**:
1. Create Azure Service Bus namespace
2. Create queues/topics:
   - `telegram-messages` (topic with subscription)
   - `trade-commands` (queue)
   - `channel-requests` (queue)
3. Implement `AzureServiceBusAdapter : IMessageBus`
4. Update DI registration in Host
5. No code changes in workers (interface remains same)

**New Capabilities**:
- Distributed processing
- Message persistence
- Dead letter queues
- Scheduled messages
- Duplicate detection
- Session-based ordering

**Configuration**:
```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://...",
    "TelegramMessagesTopic": "telegram-messages",
    "TradeCommandsQueue": "trade-commands",
    "ChannelRequestsQueue": "channel-requests",
    "MaxDeliveryCount": 3,
    "LockDuration": "00:05:00"
  }
}
```

---

## Observability

### Message Tracing

**Correlation ID**: Flows through entire pipeline

```
TelegramMessage
  └─ MessageKey: "tenant-001:123:456"
     └─ TradeCommand
        └─ CorrelationId: "tenant-001:123:456"
           └─ Broker Order
              └─ ClientOrderId: "tenant-001:123:456"
```

**Benefits**:
- Trace signal from Telegram to broker
- Link related log entries
- Performance analysis per signal

---

### Metrics to Track

**Message Bus Metrics**:
- Messages published/sec (per type)
- Messages consumed/sec (per type)
- Queue depth (backlog)
- Processing latency (p50, p95, p99)

**Pipeline Metrics**:
- Signals parsed/sec
- Signals rejected/sec (with reason)
- Trades executed/sec
- Success rate %

**Error Metrics**:
- Idempotency duplicates/sec
- Parse failures/sec
- Risk rejections/sec
- Broker errors/sec

---

## Performance Characteristics

### Throughput Targets

**Per Worker Instance**:
- Telegram messages: 100/sec
- Trade commands: 50/sec

**Per Tenant**:
- Typical: 1-10 messages/hour
- Peak: 100 messages/hour
- Extreme: 1000 messages/hour (throttle/alert)

### Latency Targets

**End-to-End (p95)**:
- Development (InMemoryBus): < 500ms
- Production (Azure Service Bus): < 1.5s

**Per Stage (p95)**:
- Telegram → Bus: < 100ms
- Bus → Handler: < 200ms
- Handler processing: < 300ms
- Handler → Executor: < 100ms
- Executor → Broker: < 800ms

---

## Summary

The message flow architecture provides:

1. **Loose Coupling**: Components don't know about each other
2. **Scalability**: Easy to add more worker instances
3. **Resilience**: Failed messages can be retried or moved to DLQ
4. **Extensibility**: New handlers can subscribe without changing publishers
5. **Observability**: Correlation IDs enable end-to-end tracing
6. **Migration Path**: Easy to switch from in-memory to distributed bus

This design supports the system's growth from MVP to production-scale SaaS platform.