# Scalability Architecture

## Overview

Pipster is designed to scale from a single tenant to thousands of tenants with minimal architectural changes. The system supports both vertical and horizontal scaling strategies.

---

## Scaling Dimensions

### Tenants
- **Current**: Supports 100s of tenants per instance
- **Target**: 10,000+ tenants across cluster
- **Constraint**: Memory per Telegram client (~10-15 MB)

### Messages
- **Current**: 100 messages/second per worker
- **Target**: 10,000 messages/second cluster-wide
- **Constraint**: Message bus throughput

### Trades
- **Current**: 50 trades/second per worker
- **Target**: 5,000 trades/second cluster-wide
- **Constraint**: Broker API rate limits

---

## Horizontal Scaling Strategy

### Stateless Workers

All worker services are designed to be stateless, enabling horizontal scaling.

```
┌──────────────────────────────────────────────┐
│           Load Balancer / K8s Service        │
└────────┬─────────────┬──────────────┬────────┘
         │             │              │
         ▼             ▼              ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│  Worker 1   │ │  Worker 2   │ │  Worker N   │
│             │ │             │ │             │
│ 300 tenants │ │ 300 tenants │ │ 300 tenants │
└─────────────┘ └─────────────┘ └─────────────┘
         │             │              │
         └─────────────┴──────────────┘
                       │
                       ▼
              ┌─────────────────┐
              │   Shared State  │
              │  - Redis Cache  │
              │  - Database     │
              │  - Blob Storage │
              └─────────────────┘
```

**Key Principles**:
- No in-memory state shared between workers
- Shared state in external stores (Redis, Database)
- Workers can be added/removed dynamically
- Auto-scaling based on load metrics

---

## Tenant Distribution

### Current: Single-Instance (Development)

All tenants managed by one worker instance.

```
Worker Instance
├── Tenant 1 (Client + Channels)
├── Tenant 2 (Client + Channels)
├── Tenant 3 (Client + Channels)
└── ...
```

**Capacity**: ~300 tenants per instance (2 GB RAM)

---

### Future: Multi-Instance with Consistent Hashing

Tenants distributed across workers using consistent hashing.

```
┌─────────────────────────────────────────┐
│      Consistent Hash Ring               │
│                                         │
│   Tenant 1 ──► Worker A                │
│   Tenant 2 ──► Worker B                │
│   Tenant 3 ──► Worker A                │
│   Tenant 4 ──► Worker C                │
│   ...                                   │
└─────────────────────────────────────────┘
```

**Algorithm**:
```
workerIndex = Hash(tenantId) % workerCount
```

**Benefits**:
- Even distribution
- Predictable tenant → worker mapping
- Minimal reshuffling when workers added/removed

**Implementation** (Future):
- Redis-based coordination
- Workers register on startup
- Heartbeat mechanism
- Rebalancing on worker changes

---

## Component-Level Scaling

### API Layer (Pipster.Api)

**Scaling Strategy**: Horizontal (stateless)

**Current Deployment**:
- Single instance
- Azure App Service (Basic tier)

**Production Deployment**:
- Multiple instances behind load balancer
- Azure App Service (Standard tier) or Container Apps
- Auto-scale rules:
  - CPU > 70% → Add instance
  - CPU < 30% → Remove instance
  - Min: 2 instances (HA)
  - Max: 10 instances

**Capacity per Instance**:
- 1,000 requests/second
- 2 vCPU, 4 GB RAM

**Bottlenecks**:
- Database connections (use connection pooling)
- Cache lookups (Redis scales independently)

---

### Telegram Worker

**Scaling Strategy**: Horizontal with tenant affinity

**Current Deployment**:
- Single instance
- Manages all tenants

**Production Deployment**:
- 10-20 instances (Kubernetes pods)
- Tenant distribution via consistent hashing
- Session state in Azure Blob Storage

**Capacity per Instance**:
- 300-500 tenants
- 100 messages/second
- 4 vCPU, 8 GB RAM

**Scaling Triggers**:
- Active tenant count
- Message throughput
- Memory usage > 80%

**Auto-Scaling Configuration (Kubernetes HPA)**:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: telegram-worker
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: telegram-worker
  minReplicas: 2
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 75
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

### Message Handler Worker

**Scaling Strategy**: Horizontal (competing consumers)

**Current Deployment**:
- Single instance
- Processes all messages

**Production Deployment**:
- 5-10 instances
- All consume from same queue
- Message bus handles load balancing

**Capacity per Instance**:
- 100 messages/second
- 2 vCPU, 4 GB RAM

**Scaling Triggers**:
- Queue depth > 1,000
- Processing latency > 500ms
- CPU > 70%

**Auto-Scaling**: Based on queue depth (Azure Service Bus metric)

---

### Trade Executor Worker

**Scaling Strategy**: Horizontal (competing consumers)

**Current Deployment**:
- Single instance
- Executes all trades

**Production Deployment**:
- 3-5 instances
- All consume from same queue
- Each instance has connector pool

**Capacity per Instance**:
- 50 trades/second
- Constrained by broker API rate limits
- 2 vCPU, 4 GB RAM

**Scaling Triggers**:
- Queue depth > 500
- Broker API latency increasing
- Trade volume surge

---

## Data Layer Scaling

### Database

**Current**: In-Memory Repositories (development)

**Production Options**:

**Option 1: Azure SQL Database**
- Vertical scaling: Basic → Standard → Premium
- Horizontal scaling: Read replicas
- Capacity: 100,000+ tenants

**Option 2: Cosmos DB**
- Auto-scaling throughput (RU/s)
- Horizontal partitioning by TenantId
- Capacity: Unlimited tenants

**Recommended**: Start with Azure SQL, migrate to Cosmos if needed

**Scaling Strategy**:
```
Phase 1 (0-1,000 tenants)
└─ Azure SQL Basic (5 DTU)

Phase 2 (1,000-10,000 tenants)
└─ Azure SQL Standard S3 (100 DTU)

Phase 3 (10,000+ tenants)
├─ Azure SQL Premium P2 (250 DTU)
└─ OR migrate to Cosmos DB
```

---

### Cache (Redis)

**Current**: In-Memory (development)

**Production**: Azure Cache for Redis

**Scaling Strategy**:
```
Phase 1 (0-1,000 tenants)
└─ Basic tier (250 MB)

Phase 2 (1,000-5,000 tenants)
└─ Standard tier (1 GB, with HA)

Phase 3 (5,000-10,000 tenants)
└─ Premium tier (6 GB, clustering enabled)

Phase 4 (10,000+ tenants)
└─ Premium tier (26 GB, 10 shards)
```

**Data Stored**:
- Idempotency keys: ~100 bytes/key × 10,000 messages/hour × 24h = ~24 MB
- Cached configs: ~10 KB/tenant × 10,000 tenants = ~100 MB
- Total: ~150 MB + overhead

**Cache Eviction**:
- Idempotency keys: 24-hour TTL
- Tenant configs: 5-minute TTL
- LRU eviction when memory full

---

### Blob Storage (Telegram Sessions)

**Current**: Local file system (development)

**Production**: Azure Blob Storage

**Scaling Strategy**: Automatic (no configuration needed)

**Capacity**:
- Session files: ~5-10 MB per tenant
- 10,000 tenants: ~100 GB
- Azure Blob: Petabyte scale available

**Access Pattern**:
- Write: On session creation (one-time)
- Read: On worker startup/reconnection
- Low frequency, high capacity

**Cost Optimization**:
- Use Cool tier for old sessions (> 30 days)
- Archive tier for inactive tenants (> 90 days)

---

### Message Bus

**Current**: InMemoryBus (single process)

**Production**: Azure Service Bus

**Scaling Strategy**:

```
Phase 1 (0-1,000 tenants)
└─ Basic tier (256 MB)

Phase 2 (1,000-5,000 tenants)
└─ Standard tier (1 GB, topics/subscriptions)

Phase 3 (5,000+ tenants)
└─ Premium tier (1-8 messaging units)
   - 1 MU = 1,000 msg/sec
   - 8 MU = 8,000 msg/sec
```

**Throughput Limits**:
- Basic: 100 msg/sec
- Standard: 1,000 msg/sec
- Premium (1 MU): 1,000 msg/sec
- Premium (8 MU): 8,000 msg/sec

**Expected Load**:
- 10,000 tenants × 10 msg/hour = 28 msg/sec (average)
- Peak: 10x = 280 msg/sec
- **Standard tier sufficient** for target scale

---

## Resource Planning

### Small Scale (< 100 tenants)

**Infrastructure**:
- 1 worker instance (2 vCPU, 4 GB RAM)
- Azure SQL Basic (5 DTU)
- Redis Basic (250 MB)
- Service Bus Basic

**Cost**: ~$50-100/month

**Capacity**:
- 100 tenants
- 1,000 messages/hour
- 100 trades/hour

---

### Medium Scale (100-1,000 tenants)

**Infrastructure**:
- 3-5 worker instances (4 vCPU, 8 GB RAM each)
- Azure SQL Standard S3 (100 DTU)
- Redis Standard (1 GB)
- Service Bus Standard

**Cost**: ~$500-1,000/month

**Capacity**:
- 1,000 tenants
- 10,000 messages/hour
- 1,000 trades/hour

---

### Large Scale (1,000-10,000 tenants)

**Infrastructure**:
- 10-20 worker instances (8 vCPU, 16 GB RAM each)
- Azure SQL Premium P2 (250 DTU) or Cosmos DB
- Redis Premium (6 GB, clustering)
- Service Bus Premium (1-2 MU)

**Cost**: ~$5,000-10,000/month

**Capacity**:
- 10,000 tenants
- 100,000 messages/hour
- 10,000 trades/hour

---

## Performance Optimization

### Caching Strategy

**Multi-Level Cache**:

```
Request
    │
    ├──► L1: In-Memory (Worker process)
    │    └─ TTL: 1 minute
    │    └─ Size: 100 MB
    │
    ├──► L2: Redis (Distributed)
    │    └─ TTL: 5 minutes
    │    └─ Size: 1 GB
    │
    └──► Database (Source of truth)
         └─ Persistent
```

**Cache Hit Rates**:
- Target: 95% cache hit rate
- Monitoring: Track cache misses per second
- Alert: If hit rate < 90%

**What to Cache**:
- Tenant configurations (high read, low write)
- Channel configurations (high read, low write)
- Trading configurations (high read, low write)
- Symbol mappings (static data)

**What NOT to Cache**:
- Broker credentials (security)
- Real-time account balances (accuracy)
- Active trades (consistency)

---

### Database Optimization

**Indexing Strategy**:
```sql
-- Tenant lookups
CREATE INDEX IX_Tenant_Email ON Tenants(Email);
CREATE INDEX IX_Tenant_Status ON Tenants(Status);

-- Channel lookups
CREATE INDEX IX_Channel_TenantId ON ChannelConfigurations(TenantId);
CREATE INDEX IX_Channel_TenantChannel ON ChannelConfigurations(TenantId, ChannelId);

-- Broker lookups
CREATE INDEX IX_Broker_TenantId ON BrokerConnections(TenantId);
CREATE INDEX IX_Broker_TenantType ON BrokerConnections(TenantId, BrokerType);
```

**Query Optimization**:
- Use EF Core query logging
- Identify N+1 queries
- Add eager loading where appropriate
- Use compiled queries for hot paths

**Connection Pooling**:
- Min pool size: 10
- Max pool size: 100
- Connection timeout: 30 seconds

---

### Async Processing

**All I/O operations are async**:
- Database queries
- Redis operations
- HTTP calls
- Message bus operations

**Benefits**:
- Higher throughput (more concurrent requests)
- Better resource utilization
- Lower latency under load

**Pattern**:
```
✅ Correct: await _repository.GetByIdAsync(id);
❌ Wrong:   _repository.GetByIdAsync(id).Result;
❌ Wrong:   _repository.GetByIdAsync(id).Wait();
```

---

## Load Testing

### Test Scenarios

**Scenario 1: Steady State**
- 1,000 tenants
- 10 messages/hour per tenant
- 24 hours duration
- **Goal**: Validate stability, no memory leaks

**Scenario 2: Peak Load**
- 1,000 tenants
- 100 messages/hour per tenant
- 1 hour duration
- **Goal**: Validate throughput, latency < 1.5s

**Scenario 3: Burst**
- 1,000 tenants
- 1,000 messages in 1 minute
- **Goal**: Validate queue handling, no message loss

**Scenario 4: Scaling**
- Start with 100 tenants
- Add 100 tenants every 10 minutes
- Up to 1,000 tenants
- **Goal**: Validate auto-scaling

### Metrics to Collect

**Throughput**:
- Messages processed/second
- Trades executed/second
- API requests/second

**Latency**:
- End-to-end (p50, p95, p99)
- Per-stage breakdown
- Queue wait time

**Resource Usage**:
- CPU utilization %
- Memory usage MB
- Network I/O MB/s
- Disk I/O ops/s

**Errors**:
- Error rate %
- Timeout count
- Retry count

---

## Monitoring & Alerting

### Key Metrics for Scaling

**Worker Count**:
- Current instance count
- Target instance count
- Scaling events/hour

**Queue Depth**:
- Messages in queue
- Messages in DLQ
- Average wait time

**Resource Utilization**:
- CPU %
- Memory %
- Network bandwidth
- Database DTU %

### Auto-Scaling Triggers

**Scale Out (Add Workers)**:
- Queue depth > 1,000 for 5 minutes
- CPU > 70% for 5 minutes
- Memory > 80% for 5 minutes

**Scale In (Remove Workers)**:
- Queue depth < 100 for 10 minutes
- CPU < 30% for 10 minutes
- Memory < 40% for 10 minutes

**Constraints**:
- Min instances: 2 (high availability)
- Max instances: 20 (cost control)
- Cooldown period: 5 minutes (prevent flapping)

---

## Capacity Planning

### Tenant Growth Projections

```
Month 1:    100 tenants
Month 3:    500 tenants
Month 6:  1,000 tenants
Month 12: 2,500 tenants
Month 24: 5,000 tenants
```

### Infrastructure Scaling Timeline

```
Q1 2025: Single instance, in-memory (MVP)
Q2 2025: Azure SQL + Redis (Beta)
Q3 2025: Multi-instance workers (GA)
Q4 2025: Auto-scaling + Service Bus (Scale)
```

### Cost Projections

```
100 tenants:      $100/month
500 tenants:      $500/month
1,000 tenants:  $1,000/month
5,000 tenants:  $5,000/month
10,000 tenants: $10,000/month
```

**Revenue Targets** (to maintain 80% margin):
- $10/tenant/month minimum
- 1,000 tenants = $10,000/month revenue - $1,000 cost = $9,000 profit

---

## Bottleneck Analysis

### Current Bottlenecks

**Memory (Telegram Workers)**:
- 10-15 MB per tenant
- Limits to ~300 tenants per instance
- **Solution**: Horizontal scaling

**Database (Reads)**:
- High read frequency for configs
- **Solution**: Redis caching (5-min TTL)

**Message Bus (Single Process)**:
- InMemoryBus not distributed
- **Solution**: Azure Service Bus

### Future Bottlenecks

**Broker API Rate Limits**:
- IG Markets: ~100 requests/minute per account
- **Solution**: Multiple accounts, request throttling

**Database Writes**:
- High write volume for audit logs
- **Solution**: Cosmos DB with auto-scaling

**Network Bandwidth**:
- High message volume
- **Solution**: Compression, efficient serialization

---

## Summary

Pipster's scalability architecture enables:

| Dimension | Current | Target | Strategy |
|-----------|---------|--------|----------|
| Tenants | 100 | 10,000+ | Horizontal scaling, consistent hashing |
| Messages/sec | 10 | 100-1,000 | Competing consumers, queue-based |
| Trades/sec | 5 | 50-500 | Parallel execution, connection pooling |
| Availability | 95% | 99.5% | Multi-instance, auto-scaling |
| Latency (p95) | 500ms | 1.5s | Caching, async processing |

**Key Success Factors**:
1. Stateless workers enable unlimited horizontal scaling
2. Distributed caching reduces database load by 95%
3. Message bus decouples components for independent scaling
4. Auto-scaling responds to load automatically
5. Multi-level caching optimizes for read-heavy workload