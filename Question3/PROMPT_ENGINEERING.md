<context>
I am the lead developer for a high-volume Payment Processing API built with
.NET 10 Minimal APIs. We are experiencing two critical production issues:

Double charging: when a client retries a timed-out request, we may end up
processing the payment twice.
Abuse: a specific merchant is flooding our API, crashing the database.

Assume multiple API instances run concurrently behind a load balancer. Assume
network timeouts and client retries are common. The solution must remain
correct, stable, and scalable under high concurrency and distributed deployment.
</context>

<task>
Design and provide a complete .NET 10 Minimal API solution with a payment
endpoint that addresses both problems above.
Produce both:

Architecture explanation justifying every design choice
Full implementable code — not theory, not placeholders

Prioritise correctness over convenience. If any assumption is needed, state it
explicitly and keep the implementation consistent with that assumption.
</task>

<root_cause_analysis>
Before writing any code, explain:

Why double charging happens in distributed systems. What failure modes
exist in naive implementations that only check "has this been processed?"
before proceeding.
Why a single abusive merchant can overwhelm the API and crash the database.
What shared resources are exhausted and why per-merchant isolation matters.
</root_cause_analysis>

<double_charge_prevention>
Design an idempotency strategy for payment requests. Address all of the
following explicitly:

How does the client signal that a request is a retry? What key is used,
where is it sent, and how is it validated server-side?
Where is the idempotency state stored? It must persist across restarts
and be visible to all instances.
How do you handle each of these scenarios:
a) First request with a new key — process normally
b) Retry after the first request succeeded — return stored response
c) Retry while the first request is still processing — must not
execute a second time
d) Retry with the same key but a different payload — must be rejected
What database schema and constraints guarantee correctness? Show the
entity design and explain why a unique constraint is necessary.
How do you prevent race conditions when two identical requests arrive
at the same millisecond across different instances?
What happens if the external payment gateway succeeds but the API crashes
before saving the result? How is this failure mode handled?
How are old idempotency records cleaned up?
</double_charge_prevention>

<abuse_prevention>
Design a rate limiting strategy scoped per merchant. Address all of the
following explicitly:

How is the merchant identified? Why should authenticated claims be
preferred over a client-supplied header?
What rate limiting algorithm do you use and why? Explain why a naive
fixed-window counter is insufficient (boundary burst problem).
Where is the rate limit state stored? It must be shared across all
instances. Explain why the operation must be atomic.
What HTTP response does a rate-limited request receive? How does the
client know when to retry?
Rate limits must be configurable per merchant — different merchants may
have different thresholds.
Describe protections at each layer:
a) API layer — rate limiting middleware
b) Application layer — per-merchant scoping
c) Database layer — connection pool limits, query timeouts
d) Infrastructure layer — mention any queueing, backpressure,
circuit-breaker, or throttling patterns if appropriate
</abuse_prevention>

<payment_endpoint_shape>
The payment endpoint (POST /api/payments) should accept at minimum:

An amount and currency
A payment method identifier (tokenised — never raw card numbers)
A merchant identifier (from auth claims or header)
An idempotency key (from header)

The response should include at minimum:

A transaction ID
A status (succeeded, failed)
The amount and currency
A timestamp

Error responses must use RFC 7807 ProblemDetails for 400, 409, 429, and 500.
Never expose stack traces or internal details.
</payment_endpoint_shape>
<security_considerations>
These are specific to payment processing:

Never log raw card numbers, CVVs, or full account numbers. Mask all
sensitive payment data in log output.
Validate the idempotency key server-side to prevent injection or
malformed values.
Merchant identity should be derived from authenticated claims where
possible — headers alone can be spoofed.
HTTPS must be enforced.
</security_considerations>

<data_consistency>
Explain clearly:

How the design remains safe under concurrent requests across instances.
Transaction boundaries — what is inside a transaction and what is not.
Why wrapping an external payment gateway call inside a database
transaction is dangerous (long-running transaction, lock contention,
connection pool exhaustion).
The correct consistency model for this payment workflow — process
externally first, then persist the result.
</data_consistency>

<implementation_requirements>
Provide complete code for all of the following. Do not omit critical wiring.
Do not leave placeholders for core logic. All code must compile logically
as a complete example and be internally consistent.

Program.cs — DI registration, middleware pipeline, endpoint mapping
Minimal API endpoint with full request flow
Request/response DTOs
DbContext and entity models with constraints
Idempotency persistence service (interface + implementation)
Rate limiter configuration and service
Payment service or handler
External payment gateway abstraction (interface + stub implementation)
Error handling approach
Cancellation support throughout
Logging and observability hooks
appsettings.json with all configurable values
</implementation_requirements>

<production_readiness>
After the code, include:

Monitoring and alerting recommendations — what metrics to track
(idempotency hit rate, rate limit rejection rate, payment latency,
gateway error rate).
How to test the solution under load and failure scenarios.
Tradeoffs and limitations of the design.
What would change if this needed to handle 10x the current volume.
</production_readiness>

<output_format>
Structure the response as:

Problem summary
Root causes
Solution overview
Architecture decisions with justifications
Database schema
Request flow (step by step)
Full .NET 10 Minimal API code (all files)
Key safeguards explained
Failure scenarios and how each is handled
Monitoring, scalability, and production notes
</output_format>

<verification>
Before finalising, verify every item. If any check fails, fix the code.

Can two simultaneous retries with the same idempotency key both execute
the payment? (Must be NO.)
Does a duplicate key return the exact original response without
re-processing? (Must be YES.)
Is a retry with the same key but different payload rejected? (Must be YES.)
Is a retry while the first request is still processing handled safely
without double execution? (Must be YES.)
If the gateway succeeds but the API crashes before saving, is the failure
mode handled on the next retry? (Must be YES.)
Does rate limiting work correctly across multiple instances? (Must be YES.)
Is the abusive merchant blocked while others continue unaffected?
(Must be YES.)
Does any error response leak a stack trace or internal detail?
(Must be NO.)
Does any log statement contain a raw card number, CVV, or account number?
(Must be NO.)
Is every configurable value sourced from configuration, not hardcoded?
(Must be YES.)
Is the external gateway call outside of any database transaction?
(Must be YES.)
Does all code compile logically as a complete, internally consistent
example? (Must be YES.)
</verification>