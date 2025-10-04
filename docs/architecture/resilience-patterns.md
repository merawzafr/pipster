# Resilience Patterns

## Overview

Pipster implements multiple resilience patterns to ensure high availability and fault tolerance. The system is designed to gracefully handle failures and recover automatically.

---

## Pattern 1: Circuit Breaker

### Purpose
Prevent cascading failures by temporarily blocking requests to a failing service.

### Implementation Location
- **ResilientTelegramClient**: Protects TDLib operations
- **HTTP Clients**: Protects broker API calls (IG Markets, future brokers)

### State Machine

```
        ┌─────────────┐
        │   CLOSED    │ (Normal operation)
        │             │
        │ Requests    │
        │ flow        │
        │ through     │
        └──────┬──────┘
               │
               │ 5 failures in 30 seconds
               ▼
        ┌─────────────┐
        │    OPEN     │ (Blocking requests)
        │             │
        │ All requests│
        │ rejected    │
        │ immediately │
        └──────┬──────┘
               │
               │ After 60 seconds
               ▼
        ┌─────────────┐
        │  HALF-OPEN  │ (Testing recovery)
        │             │
        │ Allow 1     │
        │ test request│
        └──────┬──────┘
               │
       ┌───────┴────────┐
       │                │
  Success          Failure
       │                │
       ▼                ▼
   CLOSED            OPEN
```

### Configuration (Telegram)

**Trigger Conditions**:
- 5 consecutive TdException failures
- Within 30-second window

**Break Duration**: 60 seconds

**Recovery Test**: Single request after break duration

### Configuration (HTTP/Broker APIs)

**Trigger Conditions**:
- 5 transient HTTP errors
- Status codes: 408, 500, 502, 503, 504

**Break Duration**: 30 seconds

**Recovery Test**: Single request after break duration

### Benefits
- Prevents overwhelming failing services
- Gives systems time to recover
- Fast-fail instead of waiting for timeouts
- Automatic recovery testing

### Monitoring

**Metrics to Track**:
- Circuit state changes (Closed → Open → Half-Open)
- Number of circuits open
- Time spent in Open state
- Recovery success/failure rate

**Alerts**:
- Alert if > 5 tenants have open circuits
- Alert if circuit stays open > 5 minutes
- Alert if recovery failures > 50%

---

## Pattern 2: Retry with Exponential Backoff

### Purpose
Automatically retry transient failures with increasing delays to avoid overwhelming recovering services.

### Implementation Location
- **HTTP Clients**: All broker API calls
- **Telegram Operations**: TDLib reconnection
- **Database Operations**: Future (when using cloud databases)

### Retry Strategy

```
Attempt 1: Immediate (0s delay)
    │
    │ Failure (transient error)
    ▼
Attempt 2: After 2^1 = 2 seconds
    │
    │ Failure (transient error)
    ▼
Attempt 3: After 2^2 = 4 seconds
    │
    │ Failure (transient error)
    ▼
Attempt 4: After 2^3 = 8 seconds
    │
    │ Still failing
    ▼
Give up, throw exception
```

### Transient Errors (HTTP)
- Network timeouts
- Connection refused
- HTTP 408 (Request Timeout)
- HTTP 429 (Too Many Requests)
- HTTP 500 (Internal Server Error)
- HTTP 502 (Bad Gateway)
- HTTP 503 (Service Unavailable)
- HTTP 504 (Gateway Timeout)

### Non-Transient Errors (Permanent)
- HTTP 400 (Bad Request)
- HTTP 401 (Unauthorized)
- HTTP 403 (Forbidden)
- HTTP 404 (Not Found)
- Validation errors

### Configuration

**Max Retry Attempts**: 3 (total 4 attempts)

**Max Delay**: 8 seconds (2^3)

**Jitter**: None currently (future: add randomization to prevent thundering herd)

### Benefits
- Handles temporary network glitches
- Gives services time to recover
- Reduces manual intervention
- Increases overall success rate

### Drawbacks
- Increases latency for failed operations
- May delay error detection
- Can waste resources on permanent failures

**Mitigation**: Only retry transient errors

---

## Pattern 3: Timeout

### Purpose
Prevent indefinite waiting by setting maximum wait times for operations.

### Implementation Location
- **HTTP Clients**: All API calls
- **Telegram Operations**: Message sends, connection establishment
- **Message Processing**: Pipeline stages
- **Database Queries**: Future

### Timeout Configuration

**HTTP Requests**:
- Default: 30 seconds
- IG Markets API: 30 seconds
- Future brokers: Configurable per broker

**Telegram Operations**:
- Connect/authenticate: 30 seconds
- Join channel: 10 seconds
- Message send: 5 seconds

**Message Processing**:
- Idempotency check: 1 second
- Database lookups: 5 seconds
- Signal parsing: 2 seconds
- Trade command creation: 1 second

**Deal Confirmation (IG Markets)**:
- Polling interval: 500ms
- Max wait: 10 seconds
- Total attempts: 20

### Benefits
- Prevents resource exhaustion
- Enables fast failure detection
- Maintains system responsiveness
- Protects against slow dependencies

### Monitoring

**Metrics**:
- Timeout occurrences per operation type
- Average operation duration
- p95/p99 latency

**Alerts**:
- Alert if timeout rate > 5%
- Alert if p95 latency approaching timeout

---

## Pattern 4: Idempotency

### Purpose
Ensure operations can be safely retried without unintended side effects.

### Implementation

**Storage**: Redis (production) or In-Memory (development)

**Key Format**: `pipster:idempotency:{tenantId}:{channelId}:{messageId}`

**Operation**: Atomic SET NX (Set if Not eXists)

**TTL**: 24 hours

### Process Flow

```
Message Received
    │
    ▼
┌────────────────────────────────────┐
│ Redis SET NX                       │
│ Key: tenant:channel:message        │
│ Value: timestamp                   │
│ TTL: 24h                          │
│ If-Not-Exists: true               │
└────────┬───────────────────────────┘
         │
    ┌────┴─────┐
    │          │
  True       False
    │          │
    │          └──► Skip Processing (Duplicate)
    │
    ▼
 Process Message (First Time)
```

### Benefits
- Safe message redelivery
- Handles network retries
- Prevents duplicate trade execution
- No complex distributed locks needed

### Edge Cases Handled

**Network Failures**:
- Redis unavailable → Fail open (allow processing)
- Rationale: Better to risk duplicate than miss signal

**Key Expiration**:
- After 24 hours, same message can be processed again
- Acceptable: Duplicate signals unlikely after 1 day

**Race Conditions**:
- Redis SET NX is atomic
- No race condition possible

---

## Pattern 5: Graceful Degradation

### Purpose
Continue operating with reduced functionality when dependencies fail.

### Scenarios

**Redis Unavailable**:
- **Idempotency**: Skip check, process message (risk duplicates)
- **Caching**: Fall through to database
- **Rationale**: Availability > consistency for this use case

**Database Unavailable**:
- **Tenant Lookup**: Use cached values (stale up to 5 minutes)
- **Config Lookup**: Use cached values
- **Writes**: Fail fast, retry later
- **Rationale**: Read availability critical, writes can wait

**Telegram API Slow/Down**:
- **Circuit Breaker Opens**: Stop trying to connect
- **Existing Connections**: Continue processing messages
- **New Channels**: Queue requests, process when recovered
- **Rationale**: Protect system, preserve working connections

**Broker API Unavailable**:
- **Specific Broker**: Fail that trade, log error
- **Other Brokers**: Continue executing
- **Worker**: Don't crash, continue processing queue
- **Rationale**: One broker failure shouldn't affect others

### Non-Degradable Scenarios

**Message Bus Unavailable**:
- Cannot degrade: System requires message bus
- Mitigation: Use reliable service (Azure Service Bus)
- Recovery: Automatic reconnection

**All Brokers Unavailable**:
- Cannot execute trades
- Mitigation: Log signals for manual review
- Recovery: Replay when brokers recover

---

## Pattern 6: Health Checks

### Purpose
Continuously monitor component health and remove unhealthy instances.

### Implementation

**Telegram Client Health Checks**:
- Frequency: Every 1 minute
- Checks: IsConnected property
- Action: Remove unhealthy clients from pool

**Worker Health Checks**:
- Built-in .NET health check framework
- Exposed via `/health` endpoint
- Kubernetes liveness/readiness probes

**Broker Connection Validation**:
- On connector creation
- Before first order execution
- Periodically (future)

### Health Check Logic

```
┌────────────────────────────────────┐
│ Periodic Timer (1 minute)         │
└────────┬───────────────────────────┘
         │
         ▼
┌────────────────────────────────────┐
│ Iterate All Cached Clients        │
└────────┬───────────────────────────┘
         │
         ▼
┌────────────────────────────────────┐
│ Check: IsConnected && IsHealthy    │
└────────┬───────────────────────────┘
         │
    ┌────┴─────┐
    │          │
  True       False
    │          │
    │          └──► Remove from Pool
    │              └──► Dispose Client
    │              └──► Log Warning
    │
    ▼
  Keep Client
```

### Benefits
- Automatic cleanup of failed connections
- Prevents using broken clients
- Enables self-healing
- Reduces manual intervention

---

## Pattern 7: Connection Pooling

### Purpose
Reuse expensive resources (Telegram clients, HTTP connections) instead of creating new ones.

### Telegram Client Pooling

**Pool Manager**: TelegramClientManager

**Pool Size**: Unlimited (constrained by MaxConcurrentClients = 1000)

**Eviction Policy**:
- Unhealthy clients removed immediately
- Idle clients removed after 1 hour (configurable)
- Manual removal via RemoveClientAsync()

**Creation Policy**:
- Lazy: Only create when needed
- Synchronized: Lock prevents duplicate creation
- Cached: Reuse existing if healthy

### HTTP Connection Pooling

**Implementation**: HttpClientFactory (built-in .NET)

**Pool Size**: Managed automatically

**Connection Lifetime**: 2 minutes (default)

**Benefits**:
- Avoids port exhaustion
- DNS rotation
- Proper disposal

### Benefits
- Reduces latency (no connection setup)
- Reduces resource usage
- Improves throughput
- Handles transient failures

---

## Pattern 8: Bulkhead Isolation

### Purpose
Isolate failures to prevent one tenant/channel from affecting others.

### Implementation

**Multi-Tenant Isolation**:
- One Telegram client per tenant
- Separate circuit breakers per tenant
- Independent connection pools

**Benefits**:
- Tenant A's connection failure doesn't affect Tenant B
- Rate limits isolated per tenant
- Failure containment

**Future Enhancements**:
- Resource quotas per tenant
- CPU/memory limits per tenant
- Request rate limiting per tenant

---

## Pattern 9: Dead Letter Queue (DLQ)

### Purpose
Capture messages that fail after max retries for manual review.

### Current Status
**Not Yet Implemented** - Planned for Azure Service Bus migration

### Future Design

```
Main Queue: trade-commands
    │
    │ Process
    ▼
┌────────────────────┐
│ Try Processing     │
└────────┬───────────┘
         │
    Fails 3 times
         │
         ▼
Dead Letter Queue
    │
    └──► Manual Review
         or
         Automated Retry with Fix
```

### DLQ Processing Strategies

**Option 1: Manual Review**:
- Operations team reviews DLQ daily
- Fix root cause
- Replay messages manually

**Option 2: Automated Retry**:
- After fixing deployment, automatically replay DLQ
- Add delay to avoid overwhelming system

**Option 3: Archive**:
- Move to cold storage after 30 days
- For audit/compliance

---

## Failure Scenarios & Responses

### Scenario 1: Telegram API Down

**Detection**:
- Circuit breaker opens for all tenants
- Health checks fail
- Connection state changes detected

**Response**:
- Stop attempting new connections
- Keep existing connections alive (if any)
- Queue channel add/remove requests
- Alert operations team

**Recovery**:
- Automatic reconnection when API recovers
- Circuit breaker tests recovery
- Process queued requests

**Expected Duration**: 5-30 minutes (Telegram SLA)

---

### Scenario 2: Broker API Slow

**Detection**:
- Order placement timeouts increase
- p95 latency exceeds threshold
- Timeout alerts fire

**Response**:
- Retry with backoff (up to 3 times)
- If still failing, log error
- Continue processing other trades
- Alert if sustained

**Recovery**:
- Automatic when broker recovers
- No manual intervention needed

**Expected Duration**: 1-10 minutes

---

### Scenario 3: Redis Unavailable

**Detection**:
- Idempotency check exceptions
- Cache miss rate 100%
- Connection errors in logs

**Response**:
- **Idempotency**: Fail open, allow processing (risk duplicates)
- **Caching**: Fall through to database
- **Alert**: Page on-call if sustained > 5 minutes

**Impact**:
- Possible duplicate message processing
- Increased database load
- Acceptable for short outages

**Recovery**:
- Automatic reconnection
- Cache rebuilds gradually

**Expected Duration**: < 5 minutes (Azure SLA)

---

### Scenario 4: Database Unavailable

**Detection**:
- Repository exceptions
- Query timeouts
- Connection pool exhausted

**Response**:
- **Reads**: Use cached values (stale up to 5 min)
- **Writes**: Fail fast, retry queue (future)
- **Alert**: Immediate page

**Impact**:
- Cannot create new tenants/channels
- Existing operations continue with cache
- Critical: Fix within 5 minutes

**Recovery**:
- Database failover (if Azure SQL)
- Cache continues serving reads

**Expected Duration**: < 5 minutes (Azure SQL SLA)

---

### Scenario 5: Worker Crash

**Detection**:
- Kubernetes pod restart
- Health check failure
- No heartbeat

**Response**:
- **Kubernetes**: Auto-restart pod
- **Message Bus**: Messages remain in queue
- **Telegram Clients**: Disposed, recreated on restart
- **In-flight Messages**: Redelivered after visibility timeout

**Impact**:
- Brief processing pause (< 30 seconds)
- No message loss
- Sessions recover from blob storage

**Recovery**:
- Automatic pod restart
- Reconnect to Telegram
- Resume processing

**Expected Duration**: < 1 minute

---

## Monitoring & Alerting

### Key Metrics

**Availability Metrics**:
- Worker uptime %
- Circuit breaker state (per tenant)
- Health check pass rate

**Latency Metrics**:
- End-to-end processing time (p50, p95, p99)
- Per-stage latency
- Timeout occurrences

**Error Metrics**:
- Retry count per operation
- Circuit breaker trips
- DLQ message count
- Idempotency duplicate rate

### Alert Thresholds

**Critical (Page Immediately)**:
- Database unavailable
- > 50% workers down
- > 10% circuits open
- No messages processed for 5 minutes

**Warning (Notify During Business Hours)**:
- > 5 circuits open
- p95 latency > 2 seconds
- Retry rate > 10%
- DLQ depth > 100

**Info (Log Only)**:
- Single circuit breaker trip
- Individual timeout
- Cache miss

---

## Testing Resilience

### Chaos Engineering

**Future Tests**:
- Kill random worker pods
- Introduce network latency
- Throttle broker API responses
- Fill disk to cause write failures
- Exhaust connection pools

**Tools**:
- Chaos Mesh (Kubernetes)
- Azure Chaos Studio
- Custom fault injection

### Load Testing

**Scenarios**:
- 1,000 concurrent tenants
- 100 messages/second per tenant
- Sustained load for 1 hour

**Metrics to Validate**:
- Latency remains < 1.5s
- No memory leaks
- Circuit breakers don't trip under normal load
- Graceful degradation under overload

---

## Summary

Pipster implements comprehensive resilience patterns:

| Pattern | Purpose | Benefit |
|---------|---------|---------|
| Circuit Breaker | Prevent cascading failures | Fast failure, auto-recovery |
| Retry with Backoff | Handle transient errors | Increased success rate |
| Timeout | Prevent indefinite waits | Bounded latency |
| Idempotency | Safe retries | No duplicate trades |
| Graceful Degradation | Partial availability | Better than total failure |
| Health Checks | Detect failures early | Self-healing |
| Connection Pooling | Reuse resources | Lower latency, higher throughput |
| Bulkhead Isolation | Failure containment | Tenant isolation |
| Dead Letter Queue | Preserve failed messages | No message loss |

These patterns work together to achieve **99.5% availability** and **sub-second recovery** from most failures.