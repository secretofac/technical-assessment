# Payment Processing API — Complete Solution

## 1. Problem Summary

A high-volume Payment Processing API built with .NET 10 Minimal APIs suffers from two critical production issues:

1. **Double charging** — when a client retries a timed-out request, the payment may be processed twice.
2. **Abuse** — a single merchant floods the API, exhausting database connections and crashing the system for all merchants.

The system runs multiple instances behind a load balancer with common network timeouts and client retries.

---

## 2. Root Causes

### Why Double Charging Happens

In a distributed system with multiple API instances behind a load balancer, a naive "check-then-process" approach fails because of the **TOCTOU (Time-Of-Check-to-Time-Of-Use) race condition**:

1. **Instance A** receives a payment request, checks the database — no record found.
2. **Instance B** receives the same retry 2ms later, checks the database — still no record found (Instance A hasn't committed yet).
3. Both instances proceed to charge the external payment gateway → **double charge**.

Failure modes in naive implementations:
- **Read-then-write without atomicity**: The gap between "check if processed" and "mark as processing" allows concurrent requests to both pass the check.
- **In-memory deduplication**: Only works on a single instance; a retry hitting a different instance bypasses it entirely.
- **Wrapping the gateway call in a DB transaction**: The external HTTP call makes the transaction long-running, holding locks and exhausting the connection pool — this trades correctness for availability.

### Why a Single Merchant Can Crash the Database

Without per-merchant isolation:
- **Connection pool exhaustion**: One merchant sending thousands of requests per second consumes all available DB connections. Other merchants' requests queue up and timeout.
- **Lock contention**: Massive concurrent writes from one merchant cause row/page-level lock escalation, blocking reads for everyone.
- **Thread pool starvation**: The API's thread pool is shared — one merchant's flood starves other merchants of processing capacity.
- **No backpressure**: Without rate limiting, the system accepts every request until it collapses.

Per-merchant isolation ensures that one merchant's abuse affects only their own rate limit bucket, not the shared infrastructure.

---

## 3. Solution Overview

### Idempotency Strategy (Double-Charge Prevention)

- Client sends a unique `Idempotency-Key` header with each payment request.
- The server uses a database table with a **unique constraint** on `(IdempotencyKey, MerchantId)` to guarantee exactly-once processing.
- The INSERT itself is the lock — the unique constraint means only one concurrent insert can succeed, and the loser gets a `DbUpdateException`.
- The idempotency key is also forwarded to the external payment gateway, providing defence-in-depth.

### Rate Limiting Strategy (Abuse Prevention)

- **Sliding window algorithm** partitioned per merchant identity (from authenticated JWT claims).
- ASP.NET Core's built-in `RateLimiter` middleware with `SlidingWindowRateLimiter`.
- Per-merchant configuration overrides allow premium merchants higher limits.
- Rate-limited requests receive `429 Too Many Requests` with a `Retry-After` header.

---

## 4. Architecture Decisions with Justifications

| Decision | Justification |
|---|---|
| **`IDbContextFactory` instead of scoped `DbContext`** | Each operation gets its own short-lived context. Prevents holding connections open during external gateway calls. Thread-safe for concurrent operations. |
| **Unique DB constraint for idempotency** | Atomic guarantee at the database level. No distributed lock needed. Works across all instances without coordination. |
| **Gateway call OUTSIDE database transactions** | An external HTTP call inside a transaction holds locks for seconds, causing connection pool exhaustion and deadlocks. Process externally first, then persist. |
| **Release-on-failure pattern** | If the gateway call or post-gateway persistence fails, the idempotency key is released, allowing clean retry. Gateway-level idempotency (forwarded key) prevents double-charge even if we re-call the gateway. |
| **Sliding window over fixed window** | Fixed window has the boundary-burst problem: a merchant can double their throughput at window boundaries. Sliding window smooths this out across segments. |
| **Merchant identity from JWT claims** | Headers can be spoofed. Authenticated claims are verified by the auth middleware and cannot be forged by the client. |
| **SHA-256 request hash** | Detects when a client reuses an idempotency key with a different payload — a contract violation that must be rejected (409). |
| **Background cleanup service** | Idempotency records accumulate over time. Periodic cleanup prevents table bloat while retaining records long enough for legitimate retries. |
| **RFC 7807 ProblemDetails for all errors** | Industry-standard error format. Never leaks stack traces or internal details. |

---

## 5. Database Schema

### IdempotencyRecords

| Column | Type | Constraints |
|---|---|---|
| Id | GUID | PK |
| IdempotencyKey | nvarchar(128) | NOT NULL |
| MerchantId | nvarchar(64) | NOT NULL |
| RequestHash | nvarchar(64) | NOT NULL |
| Status | nvarchar(16) | NOT NULL (Processing/Succeeded/Failed) |
| ResponseBody | nvarchar(max) | NULL (populated on completion) |
| HttpStatusCode | int | NULL |
| CreatedAtUtc | datetimeoffset | NOT NULL |
| UpdatedAtUtc | datetimeoffset | NOT NULL |

**Unique Index**: `IX_Idempotency_Key_Merchant` on `(IdempotencyKey, MerchantId)` — this is the critical constraint that prevents double processing.

### PaymentRecords

| Column | Type | Constraints |
|---|---|---|
| Id | GUID | PK |
| MerchantId | nvarchar(64) | NOT NULL, indexed |
| Amount | decimal(18,4) | NOT NULL |
| Currency | nvarchar(3) | NOT NULL |
| PaymentMethodToken | nvarchar(256) | NOT NULL |
| Description | nvarchar(max) | NULL |
| Status | nvarchar(16) | NOT NULL |
| GatewayTransactionId | nvarchar(max) | NULL |
| FailureReason | nvarchar(max) | NULL |
| IdempotencyKey | nvarchar(128) | NOT NULL, indexed |
| CreatedAtUtc | datetimeoffset | NOT NULL |

---

## 6. Request Flow (Step by Step)

```
Client → POST /api/payments
         Headers: Idempotency-Key: abc-123, X-Merchant-Id: merchant-001
         Body: { amount: 99.99, currency: "USD", paymentMethodToken: "tok_xxx" }

1. Rate Limiter Middleware
   → Check sliding window for merchant-001
   → If exceeded → 429 + Retry-After header (STOP)
   → If within limit → continue

2. Endpoint Validation
   → Validate Idempotency-Key format (alphanumeric, hyphens, underscores, ≤128 chars)
   → Validate merchant identity from claims (preferred) or header
   → Validate request body (amount > 0, currency 3 chars, token present)
   → If invalid → 400 ProblemDetails (STOP)

3. Idempotency Check (PaymentService.ProcessPaymentAsync)
   → Compute SHA-256 hash of request body
   → Try to INSERT IdempotencyRecord with status=Processing
     a) INSERT succeeds → key claimed, proceed to step 4
     b) INSERT fails (unique constraint violation) → fetch existing record:
        - Different RequestHash → 409 Conflict (key reuse with different payload)
        - Status=Processing → 409 Conflict (request in progress, retry later)
        - Status=Succeeded/Failed → return cached ResponseBody (idempotent replay)

4. External Gateway Call (OUTSIDE any DB transaction)
   → Call IPaymentGateway.ChargeAsync with forwarded idempotency key
   → If gateway throws → release idempotency key, re-throw → 500

5. Persist Payment Result
   → Insert PaymentRecord with gateway result
   → Update IdempotencyRecord: status=Succeeded/Failed, store serialised response

6. Return Response
   → 201 Created (success) or 200 OK (failure/cached)
   → Body: { transactionId, status, amount, currency, timestamp }
```

---

## 7. Code Structure

```
src/PaymentApi/
├── Program.cs                              — DI, middleware pipeline, endpoint
├── PaymentApi.csproj                       — Project file with dependencies
├── appsettings.json                        — All configurable values
├── appsettings.Development.json            — Development overrides
├── Configuration/
│   ├── RateLimitOptions.cs                 — Rate limiting settings
│   ├── IdempotencyOptions.cs               — Idempotency settings
│   ├── PaymentGatewayOptions.cs            — Gateway settings
│   └── DatabaseOptions.cs                  — DB pool/timeout settings
├── Data/
│   ├── PaymentDbContext.cs                 — EF Core context with constraints
│   ├── IdempotencyRecord.cs                — Idempotency entity
│   └── PaymentRecord.cs                    — Payment entity
├── Middleware/
│   ├── GlobalExceptionHandler.cs           — RFC 7807 error handler
│   └── MerchantRateLimitPolicy.cs          — Sliding window rate limiter
├── Models/
│   ├── PaymentRequest.cs                   — Inbound DTO
│   ├── PaymentResponse.cs                  — Outbound DTO
│   └── GatewayResult.cs                    — Gateway response DTO
└── Services/
    ├── IPaymentGateway.cs                  — Gateway abstraction
    ├── StubPaymentGateway.cs               — Dev/test stub
    ├── IIdempotencyService.cs              — Idempotency contract
    ├── IdempotencyService.cs               — Idempotency implementation
    ├── IPaymentService.cs                  — Payment orchestration contract
    ├── PaymentService.cs                   — Payment orchestration
    └── IdempotencyCleanupService.cs        — Background cleanup
```

---

## 8. Key Safeguards

| Safeguard | Implementation |
|---|---|
| **No double charge** | Unique DB constraint on (Key, Merchant) — only one INSERT wins |
| **Concurrent request safety** | The INSERT is the lock — no TOCTOU gap |
| **Retry with same payload** | Returns cached response from IdempotencyRecord |
| **Retry with different payload** | Rejected with 409 via SHA-256 hash comparison |
| **Retry while processing** | Returns 409 "in progress" — does not execute a second time |
| **Gateway succeeds, API crashes** | Key released on exception; gateway's own idempotency key prevents re-charge on retry |
| **No sensitive data in logs** | Payment tokens masked, stack traces never in responses |
| **HTTPS enforced** | HSTS + HTTPS redirection in middleware pipeline |
| **No hardcoded secrets** | All values from `IConfiguration` / `IOptions<T>`, connection strings from env vars |

---

## 9. Failure Scenarios

| Scenario | Handling |
|---|---|
| Client retries after success | Cached response returned from IdempotencyRecord (HTTP 200/201) |
| Client retries during processing | 409 "Request In Progress" — client should back off and retry |
| Client reuses key with different body | 409 "Idempotency Key Conflict" — contract violation |
| Gateway timeout | Exception caught → idempotency key released → client retries → gateway idempotency key prevents re-charge |
| API crash after gateway success | Same as gateway timeout — key released, retry safe via gateway-level idempotency |
| DB connection exhausted | EF Core retry policy with exponential backoff; connection pool limits prevent runaway consumption |
| Merchant rate limit exceeded | 429 with Retry-After header; other merchants unaffected |
| Malformed idempotency key | 400 ProblemDetails — rejected before any processing |

---

## 10. Monitoring, Scalability, and Production Notes

### Metrics to Track

- **Idempotency hit rate**: `idempotency_cache_hit / total_requests` — high rate indicates clients are retrying frequently (investigate timeouts).
- **Rate limit rejection rate**: per merchant — spike means abuse or misconfigured limits.
- **Payment latency (p50, p95, p99)**: end-to-end from request to response.
- **Gateway error rate**: failures from the external payment provider.
- **Gateway latency**: p95/p99 to detect degradation before it causes client timeouts.
- **DB connection pool utilisation**: approaching max pool size is an early warning.
- **Idempotency table size**: ensure cleanup service is keeping up.

### Load Testing Recommendations

1. **Concurrent duplicate requests**: Send N identical requests (same idempotency key) simultaneously across multiple instances. Verify exactly one payment is created.
2. **Rate limit accuracy**: Send requests above the configured limit for a single merchant. Verify 429s start at the expected threshold. Verify other merchants are unaffected.
3. **Gateway failure simulation**: Inject gateway timeouts/errors. Verify idempotency keys are released and retries succeed.
4. **Crash simulation**: Kill an API instance mid-request (after gateway call, before DB save). Verify the next retry recovers correctly.
5. **Connection pool exhaustion**: Simulate high concurrent load. Verify the system degrades gracefully (429s/503s) rather than crashing.

### Tradeoffs and Limitations

- **SQL Server as idempotency store**: Adds write latency vs. Redis. Chosen for transactional consistency and simplicity — no separate infrastructure. For higher throughput, consider Redis with Lua scripts for atomic claim.
- **In-process rate limiting**: ASP.NET Core's built-in `RateLimiter` is per-instance, not distributed. For true cross-instance rate limiting, replace with Redis-backed sliding window (e.g., `StackExchange.Redis` with Lua scripts). The current approach still provides per-instance protection.
- **Release-on-failure**: If the API crashes hard (process kill) before releasing the key, the key stays in "Processing" state. Mitigation: a background job that resets "Processing" keys older than a configurable threshold (e.g., 5 minutes).
- **No distributed lock**: The design intentionally avoids distributed locks (Redis/ZooKeeper) in favour of the database unique constraint. This is simpler and sufficient for most payment volumes.

### Scaling to 10x Volume

1. **Distributed rate limiting**: Replace in-process `RateLimiter` with Redis-backed sliding window shared across all instances.
2. **Read replicas**: Route idempotency lookups (the "is this a retry?" check) to read replicas; write the claim to primary.
3. **Async payment processing**: For very high volume, accept the payment request synchronously (return 202 Accepted), process asynchronously via a message queue (Azure Service Bus, RabbitMQ), and provide a status endpoint for polling.
4. **Database sharding**: Partition IdempotencyRecords by MerchantId hash for horizontal scaling.
5. **Redis for idempotency state**: Move the hot path (claim/check) to Redis with Lua scripts for sub-millisecond atomicity. Use SQL Server as the durable record.
6. **Circuit breakers**: Add Polly circuit breakers on the gateway client to fail fast when the gateway is degraded, preventing cascading failures.
