using PaymentApi.Configuration;
using PaymentApi.Data;
using PaymentApi.Middleware;
using PaymentApi.Models;
using PaymentApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration binding
// ---------------------------------------------------------------------------
// TODO: Bind IOptions<T> for:
//   - RateLimitOptions
//   - IdempotencyOptions
//   - PaymentGatewayOptions
//   - DatabaseOptions

// ---------------------------------------------------------------------------
// Database — EF Core with SQL Server
// ---------------------------------------------------------------------------
// TODO: Register IDbContextFactory<PaymentDbContext>
//   - Connection string from IConfiguration
//   - CommandTimeout from DatabaseOptions
//   - EnableRetryOnFailure for transient fault handling

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
// TODO: Register:
//   - IIdempotencyService → IdempotencyService (Scoped)
//   - IPaymentService → PaymentService (Scoped)
//   - IPaymentGateway → StubPaymentGateway (Singleton)

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
// TODO: Register IdempotencyCleanupService as hosted service

// ---------------------------------------------------------------------------
// Rate limiting — sliding window, per-merchant
// ---------------------------------------------------------------------------
// TODO: Call AddMerchantRateLimiting() extension method

// ---------------------------------------------------------------------------
// Exception handling — RFC 7807 ProblemDetails
// ---------------------------------------------------------------------------
// TODO: Register GlobalExceptionHandler and ProblemDetails

// ---------------------------------------------------------------------------
// HTTP client for payment gateway (with Polly resilience)
// ---------------------------------------------------------------------------
// TODO: Register named HttpClient "PaymentGateway"
//   - BaseAddress and Timeout from PaymentGatewayOptions
//   - AddStandardResilienceHandler() for retry/circuit-breaker

// ---------------------------------------------------------------------------
// HTTPS enforcement
// ---------------------------------------------------------------------------
// TODO: Configure HSTS

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline (order matters)
// ---------------------------------------------------------------------------
// TODO: Wire up in order:
//   1. app.UseExceptionHandler()
//   2. app.UseHsts() (non-development only)
//   3. app.UseHttpsRedirection()
//   4. app.UseRateLimiter()

// ---------------------------------------------------------------------------
// Payment endpoint — POST /api/payments
// ---------------------------------------------------------------------------
// TODO: Map endpoint with:
//   - [FromBody] PaymentRequest
//   - [FromHeader] Idempotency-Key
//   - Merchant ID from claims or X-Merchant-Id header
//   - CancellationToken
//
// Validation:
//   - Idempotency key: required, max length, alphanumeric/hyphens/underscores
//   - Merchant ID: required (prefer claims over header)
//   - Amount: must be > 0
//   - Currency: must be 3-letter ISO 4217
//   - PaymentMethodToken: required
//
// All validation failures return RFC 7807 ProblemDetails (400).
//
// On success: delegate to IPaymentService.ProcessPaymentAsync
// Apply .RequireRateLimiting(MerchantRateLimitPolicy.PolicyName)

app.Run();
