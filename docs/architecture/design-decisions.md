# Design Decisions

## Overview

This document captures key architectural and design decisions made during Pipster's development, including the rationale, alternatives considered, and trade-offs.

---

## ADR-001: Clean Architecture with Onion Layers

**Status**: Accepted

**Date**: 2024-12

### Context

Need to structure the codebase for long-term maintainability, testability, and flexibility to change infrastructure components.

### Decision

Implement Clean Architecture with strict dependency rules:
- Domain layer has zero dependencies
- Application layer depends only on Domain
- Infrastructure implements Domain interfaces
- Presentation/Host wires everything together

### Rationale

**Benefits**:
- Business logic isolated and testable
- Infrastructure can be swapped without changing domain
- Clear separation of concerns
- Framework independence

**Drawbacks**:
- More initial complexity
- More files and abstractions
- Steeper learning curve for new developers

### Alternatives Considered

**Traditional Layered Architecture**:
- Simpler to understand
- Rejected: Tight coupling to infrastructure

**Vertical Slice Architecture**:
- Feature-focused organization
- Rejected: Harder to enforce cross-cutting concerns

### Consequences

- Requires discipline to maintain boundaries
- Repository pattern mandatory for data access
- More unit tests possible without infrastructure

---

## ADR-002: Multi-Tenant with Isolated Telegram Clients

**Status**: Accepted

**Date**: 2024-12

### Context

Need to support multiple tenants monitoring Telegram channels simultaneously without interference.

### Decision

Create one TDLib client instance per tenant, managed in a connection pool.

### Rationale

**Benefits**:
- Complete tenant isolation (no cross-tenant contamination)
- Rate limit isolation (one tenant's spam doesn't affect others)
- Fault isolation (one tenant's failure doesn't crash others)
- Independent session management

**Drawbacks**:
- Higher memory usage (~10-15 MB per tenant)
- More complex connection management
- Scaling limited by memory

### Alternatives Considered

**Single Shared TDLib Client**:
- Lower memory footprint
- Rejected: Cross-tenant interference, rate limit issues

**One Client per Channel**:
- Even more isolation
- Rejected: Excessive resource usage

### Consequences

- Worker instances limited to ~300-500 tenants each
- Need horizontal scaling for > 1,000 tenants
- Session files must be persisted externally (Azure Blob)

---

## ADR-003: Message Bus for Async Communication

**Status**: Accepted

**Date**: 2024-12

### Context

Components need to communicate without tight coupling. System must scale horizontally.

### Decision

Use message bus (InMemoryBus → Azure Service Bus) for async communication between workers.

### Rationale

**Benefits**:
- Loose coupling between components
- Easy horizontal scaling (competing consumers)
- Natural async processing
- Message persistence and retry

**Drawbacks**:
- Added complexity
- Eventual consistency instead of immediate
- Need to handle message failures

### Alternatives Considered

**Direct Method Calls**:
- Simpler, synchronous
- Rejected: Tight coupling, no scalability

**Database as Queue**:
- Simple implementation
- Rejected: Poor performance, polling overhead

**RabbitMQ**:
- Feature-rich, proven
- Rejected: Prefer Azure-native for deployment simplicity

### Consequences

- Need idempotency checks
- Message contracts become public API
- Easier to migrate to Azure Service Bus later

---

## ADR-004: Factory Pattern for Broker Connectors

**Status**: Accepted

**Date**: 2024-12

### Context

Need to support multiple brokers without workers knowing about specific implementations.

### Decision

Use factory pattern with provider registration:
- ITradeConnector interface
- ITradeConnectorProvider per broker
- TradeConnectorFactory manages lifecycle
- Host registers providers via DI

### Rationale

**Benefits**:
- Workers completely agnostic of brokers
- Add new brokers without changing existing code
- Easy to mock for testing
- Centralized connector lifecycle management

**Drawbacks**:
- More abstraction layers
- Slightly more complex than direct instantiation

### Alternatives Considered

**Direct Instantiation**:
- Simpler
- Rejected: Workers would need to know about all brokers

**Service Locator**:
- Less dependency injection
- Rejected: Anti-pattern, harder to test

### Consequences

- Adding new broker = 3 steps: implement connector, create provider, register in Host
- Workers remain unchanged when brokers added

---

## ADR-005: Cached Configuration with 5-Minute TTL

**Status**: Accepted

**Date**: 2024-12

### Context

Every message requires loading tenant/channel/trading configuration. Database queries would be bottleneck.

### Decision

Implement multi-level cache:
- L1: In-memory (1-minute TTL) per worker
- L2: Redis (5-minute TTL) shared across workers

### Rationale

**Benefits**:
- Reduces database load by 95%+
- Sub-millisecond lookup time
- Acceptable staleness for configuration data

**Drawbacks**:
- Configuration changes take up to 5 minutes to propagate
- Cache invalidation complexity
- Memory usage for cache

### Alternatives Considered

**No Caching**:
- Always fresh data
- Rejected: Database would be overwhelmed

**Long TTL (1 hour)**:
- Less database load
- Rejected: Too stale for user experience

**Event-Based Invalidation**:
- Immediate propagation
- Rejected: Complexity not worth marginal benefit

### Consequences

- Configuration changes require manual cache invalidation for immediate effect
- Need cache hit rate monitoring
- Acceptable trade-off for performance

---

## ADR-006: Idempotency with Redis SET NX

**Status**: Accepted

**Date**: 2024-12

### Context

Message bus may deliver same message twice. Must prevent duplicate trade execution.

### Decision

Use Redis SET NX (atomic set-if-not-exists) with 24-hour TTL for deduplication.

### Rationale

**Benefits**:
- Atomic operation (no race conditions)
- Distributed (works across workers)
- Simple implementation
- Automatic cleanup via TTL

**Drawbacks**:
- Requires Redis dependency
- Additional latency (~1-5ms per check)
- Fail-open if Redis unavailable (risk duplicates)

### Alternatives Considered

**Database Unique Constraint**:
- Persistent
- Rejected: Slower, pollutes database

**In-Memory HashSet**:
- Fast
- Rejected: Not distributed, lost on restart

**Distributed Lock**:
- More complex patterns possible
- Rejected: Overkill for this use case

### Consequences

- Redis is critical dependency
- Message processing latency +1-5ms
- Acceptable risk of duplicates if Redis down (fail-open)

---

## ADR-007: Session Persistence in Azure Blob Storage

**Status**: Accepted

**Date**: 2024-12

### Context

Telegram sessions must survive worker restarts. Sessions are 5-10 MB per tenant.

### Decision

Store session files in Azure Blob Storage with local caching.

### Rationale

**Benefits**:
- Durable (survives restarts)
- Scalable (unlimited capacity)
- Cost-effective (~$0.02/GB/month)
- Automatic geo-replication

**Drawbacks**:
- Slower than local disk (network I/O)
- Additional Azure dependency

### Alternatives Considered

**Local Disk Only**:
- Fastest
- Rejected: Lost on worker restart

**Database**:
- Simpler
- Rejected: Inefficient for binary files

**Network File Share**:
- Persistent
- Rejected: More expensive, less scalable

### Consequences

- Worker startup time +2-5 seconds (download sessions)
- Need lifecycle management (archive old sessions)
- Local cache required for performance

---

## ADR-008: Repository Pattern for Data Access

**Status**: Accepted

**Date**: 2024-12

### Context

Need abstraction over data storage to enable testing and future migrations.

### Decision

Define repository interfaces in Domain, implement in Infrastructure.

### Rationale

**Benefits**:
- Domain doesn't know about database technology
- Easy to swap implementations (in-memory → SQL → Cosmos)
- Testable without database
- Clear contract for data access

**Drawbacks**:
- More interfaces and classes
- Potential for leaky abstractions

### Alternatives Considered

**Direct EF Core Usage**:
- Less abstraction
- Rejected: Domain coupled to EF Core

**Generic Repository**:
- Less code
- Rejected: Loses type safety, unclear operations

### Consequences

- Each entity gets its own repository interface
- Need in-memory implementations for testing
- Easier migration path to different storage

---

## ADR-009: Regex-Based Signal Parsing

**Status**: Accepted (MVP), Review Later

**Date**: 2024-12

### Context

Need to extract trading signals from free-form Telegram messages.

### Decision

Use configurable regex patterns per channel, applied by RegexSignalParser.

### Rationale

**Benefits**:
- Flexible (each channel can have different format)
- Configurable without code changes
- Fast (compiled regex)
- Simple to implement

**Drawbacks**:
- Fragile (breaks if format changes)
- User needs regex knowledge
- Limited error messages
- No semantic understanding

### Alternatives Considered

**Natural Language Processing (NLP)**:
- More robust
- Rejected: Overkill for MVP, slower, more complex

**Fixed Format Parser**:
- Simpler
- Rejected: Not flexible enough for different signal providers

**ML-Based Extraction**:
- Most robust
- Rejected: Requires training data, too complex for MVP

### Consequences

- Users must provide correct regex patterns
- Need good regex testing tools
- Consider ML-based parser for v2.0

---

## ADR-010: Circuit Breaker for External Services

**Status**: Accepted

**Date**: 2024-12

### Context

External services (Telegram, brokers) can fail. Need to prevent cascading failures.

### Decision

Use Polly circuit breaker:
- 5 failures in 30 seconds → open circuit
- 60-second break duration
- Automatic recovery testing

### Rationale

**Benefits**:
- Fast failure instead of timeouts
- Gives services time to recover
- Prevents resource exhaustion
- Automatic recovery

**Drawbacks**:
- False positives possible
- Need tuning for each service

### Alternatives Considered

**No Circuit Breaker**:
- Simpler
- Rejected: Risk of cascading failures

**Manual Circuit Control**:
- More control
- Rejected: Not automated

### Consequences

- Need monitoring of circuit breaker states
- Alert when too many circuits open
- Some legitimate requests rejected during break

---

## ADR-011: InMemoryBus for MVP, Service Bus for Production

**Status**: Accepted

**Date**: 2024-12

### Context

Need message bus for async communication. Production needs persistence and distribution.

### Decision

Use InMemoryBus (System.Threading.Channels) for MVP, migrate to Azure Service Bus for production.

### Rationale

**Benefits**:
- Fast development with InMemoryBus
- No external dependency for MVP
- Easy migration (same interface)
- Production-ready with Service Bus

**Drawbacks**:
- InMemoryBus lost on restart
- Need migration work for production

### Alternatives Considered

**Start with Service Bus**:
- Production-ready immediately
- Rejected: Slower development, requires Azure resources

**RabbitMQ**:
- Open source
- Rejected: Prefer Azure-native

### Consequences

- Interface design critical (must work for both)
- Migration work required for production
- Clear path to scale

---

## ADR-012: Fail-Open for Cache/Idempotency on Errors

**Status**: Accepted

**Date**: 2024-12

### Context

Redis (cache/idempotency) might be unavailable. Need to decide: fail-open or fail-closed?

### Decision

Fail-open: Continue processing if Redis unavailable.

### Rationale

**Benefits**:
- Availability over consistency
- System remains operational
- Better user experience

**Drawbacks**:
- Risk of duplicate processing (if Redis down)
- Stale cache data
- Higher database load

### Alternatives Considered

**Fail-Closed**:
- Stronger guarantees
- Rejected: Total system outage if Redis down

**Hybrid Approach**:
- Fail-open for cache, fail-closed for idempotency
- Rejected: Inconsistent, confusing

### Consequences

- Acceptable risk: duplicate messages rare, Redis reliable
- Need monitoring and alerts
- Document behavior for operators

---

## Summary Table

| Decision | Rationale | Trade-off |
|----------|-----------|-----------|
| Clean Architecture | Testability, flexibility | More complexity |
| Multi-Tenant Clients | Isolation, fault containment | Higher memory usage |
| Message Bus | Scalability, decoupling | Eventual consistency |
| Factory Pattern | Extensibility | More abstractions |
| Cached Config (5min TTL) | Performance | Stale data |
| Redis Idempotency | Distributed, atomic | External dependency |
| Blob Session Storage | Durable, scalable | Network latency |
| Repository Pattern | Technology independence | More interfaces |
| Regex Parsing | Flexible, fast | Fragile |
| Circuit Breaker | Fault tolerance | False positives |
| InMemory → Service Bus | Fast MVP, scalable prod | Migration work |
| Fail-Open on Errors | Availability | Risk of duplicates |

---

## Decision Review Process

### When to Review

- Major version releases (1.0, 2.0, etc.)
- Significant scale changes (10x growth)
- New requirements that conflict with decisions
- Performance issues traced to architectural choices

### How to Propose Changes

1. Document problem with current decision
2. Propose alternative with rationale
3. Analyze impact and migration cost
4. Review with team
5. Update this document if accepted

### Recent Reviews

- None yet (initial version)

---

## Future Decisions Needed

### Under Consideration

**Database: SQL vs. Cosmos DB**
- Current: InMemory → Azure SQL planned
- Question: When to migrate to Cosmos DB?
- Trigger: Event volume > 1M/day or global distribution needed

**Parsing: Regex vs. ML**
- Current: Regex-based
- Question: When to add ML-based parsing?
- Trigger: User complaints about regex complexity

**Broker Failover**
- Current: Single broker per trade
- Question: Primary/backup broker strategy?
- Trigger: Broker reliability issues

**Multi-Region Deployment**
- Current: Single Azure region
- Question: When to go multi-region?
- Trigger: Latency requirements or compliance needs

---

## Lessons Learned

### What Worked Well

1. **Clean Architecture**: Easy to test, easy to swap implementations
2. **Factory Pattern**: Added IG Markets without touching workers
3. **Message Bus Interface**: Smooth path from InMemory to Service Bus
4. **Repository Pattern**: Switched from in-memory to SQL without domain changes

### What Could Be Improved

1. **Caching Strategy**: 5-minute TTL might be too long for some config changes
2. **Error Handling**: Need better structured exceptions across layers
3. **Observability**: Should have added more metrics from day one

### What to Avoid

1. **Don't**: Mix business logic in infrastructure layer
2. **Don't**: Skip cache invalidation strategy
3. **Don't**: Assume external services are always available
4. **Don't**: Over-engineer before validating need

---

## Architectural Principles

### Core Principles

1. **Dependency Inversion**: Depend on abstractions, not concretions
2. **Single Responsibility**: Each class does one thing well
3. **Open/Closed**: Open for extension, closed for modification
4. **Fail Fast**: Detect errors early, don't hide them
5. **Optimize for Change**: Code will change, make it easy

### Non-Functional Requirements

**Performance**:
- p95 latency < 1.5 seconds (Telegram → Broker)
- Cache hit rate > 95%

**Reliability**:
- 99.5% success rate
- Auto-recovery from failures

**Scalability**:
- Support 10,000+ tenants
- Horizontal scaling without code changes

**Security**:
- Encryption at rest and in transit
- Tenant data isolation
- Principle of least privilege

**Maintainability**:
- Clear layer boundaries
- Comprehensive documentation
- Automated testing

---

## References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Polly Resilience Framework](https://github.com/App-vNext/Polly)
- [Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/)

---

**Last Updated**: 2025-01-06  
**Next Review**: 2025-Q2 (after production deployment)