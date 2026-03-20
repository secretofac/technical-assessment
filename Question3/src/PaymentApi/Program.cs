using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentApi.Configuration;
using PaymentApi.Data;
using PaymentApi.Middleware;
using PaymentApi.Models;
using PaymentApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration binding
// ---------------------------------------------------------------------------
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<IdempotencyOptions>(
    builder.Configuration.GetSection(IdempotencyOptions.SectionName));
builder.Services.Configure<PaymentGatewayOptions>(
    builder.Configuration.GetSection(PaymentGatewayOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

// ---------------------------------------------------------------------------
// Database — EF Core with SQL Server
// ---------------------------------------------------------------------------
// Using IDbContextFactory for thread-safety: each operation gets its own
// short-lived DbContext, preventing concurrency issues and avoiding holding
// connections open during external gateway calls.
var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

builder.Services.AddDbContextFactory<PaymentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddSingleton<IPaymentGateway, StubPaymentGateway>();

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<IdempotencyCleanupService>();

// ---------------------------------------------------------------------------
// Rate limiting — sliding window, per-merchant
// ---------------------------------------------------------------------------
builder.Services.AddMerchantRateLimiting();

// ---------------------------------------------------------------------------
// Exception handling — RFC 7807 ProblemDetails
// ---------------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---------------------------------------------------------------------------
// HTTP client for payment gateway (with resilience via Polly)
// ---------------------------------------------------------------------------
var gatewayOptions = builder.Configuration
    .GetSection(PaymentGatewayOptions.SectionName)
    .Get<PaymentGatewayOptions>() ?? new PaymentGatewayOptions();

builder.Services.AddHttpClient("PaymentGateway", client =>
{
    client.BaseAddress = new Uri(gatewayOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(gatewayOptions.TimeoutSeconds);
})
.AddStandardResilienceHandler();

// ---------------------------------------------------------------------------
// HTTPS enforcement
// ---------------------------------------------------------------------------
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline (order matters)
// ---------------------------------------------------------------------------
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

// ---------------------------------------------------------------------------
// Payment endpoint — POST /api/payments
// ---------------------------------------------------------------------------
app.MapPost("/api/payments", async (
    HttpContext httpContext,
    [FromBody] PaymentRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
    [FromServices] IPaymentService paymentService,
    [FromServices] IOptions<IdempotencyOptions> idempotencyOptions,
    CancellationToken cancellationToken) =>
{
    // --- Validate idempotency key ---
    if (string.IsNullOrWhiteSpace(idempotencyKeyHeader))
    {
        return Results.Problem(
            title: "Missing Idempotency Key",
            detail: "The Idempotency-Key header is required.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    var idempotencyKey = idempotencyKeyHeader.Trim();
    var maxKeyLength = idempotencyOptions.Value.MaxKeyLength;

    if (idempotencyKey.Length > maxKeyLength)
    {
        return Results.Problem(
            title: "Invalid Idempotency Key",
            detail: $"Idempotency key must not exceed {maxKeyLength} characters.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    // Reject keys with injection-risk characters. Allow alphanumeric, hyphens, underscores.
    if (!IdempotencyKeyPattern().IsMatch(idempotencyKey))
    {
        return Results.Problem(
            title: "Invalid Idempotency Key",
            detail: "Idempotency key must contain only alphanumeric characters, hyphens, and underscores.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    // --- Resolve merchant identity ---
    var merchantId = httpContext.User.FindFirstValue("merchant_id")
                     ?? httpContext.Request.Headers["X-Merchant-Id"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(merchantId))
    {
        return Results.Problem(
            title: "Missing Merchant Identity",
            detail: "Merchant identity could not be resolved from claims or X-Merchant-Id header.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    // --- Validate request body ---
    if (request.Amount <= 0)
    {
        return Results.Problem(
            title: "Invalid Amount",
            detail: "Payment amount must be greater than zero.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
    {
        return Results.Problem(
            title: "Invalid Currency",
            detail: "Currency must be a valid 3-letter ISO 4217 code.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    if (string.IsNullOrWhiteSpace(request.PaymentMethodToken))
    {
        return Results.Problem(
            title: "Missing Payment Method",
            detail: "A tokenised payment method identifier is required.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400");
    }

    // --- Process payment ---
    var (response, httpStatusCode) = await paymentService.ProcessPaymentAsync(
        merchantId, idempotencyKey, request, cancellationToken);

    return Results.Json(response, statusCode: httpStatusCode);
})
.RequireRateLimiting(MerchantRateLimitPolicy.PolicyName)
.WithName("ProcessPayment")
.WithOpenApi();

app.Run();

// Source-generated regex for idempotency key validation — avoids runtime compilation cost.
partial class Program
{
    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex IdempotencyKeyPattern();
}
