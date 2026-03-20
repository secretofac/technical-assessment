using Microsoft.EntityFrameworkCore;
using PaymentApi.Data;
using PaymentApi.Models;

namespace PaymentApi.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDbContextFactory<PaymentDbContext> _dbContextFactory;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IIdempotencyService idempotencyService,
        IPaymentGateway paymentGateway,
        IDbContextFactory<PaymentDbContext> dbContextFactory,
        ILogger<PaymentService> logger)
    {
        _idempotencyService = idempotencyService;
        _paymentGateway = paymentGateway;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public Task<(PaymentResponse Response, int HttpStatusCode)> ProcessPaymentAsync(
        string merchantId,
        string idempotencyKey,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Implementation steps:
        //   1. Compute SHA-256 hash of request for payload-change detection
        //   2. TryClaimKeyAsync → if existing record returned, handle:
        //      a) Different RequestHash → throw IdempotencyConflictException (409)
        //      b) Status == Processing → throw IdempotencyInProgressException (409)
        //      c) Status == Succeeded/Failed → return cached ResponseBody
        //   3. Call gateway OUTSIDE any DB transaction
        //   4. Persist PaymentRecord
        //   5. CompleteAsync on idempotency record with serialised response
        //   6. On exception: ReleaseAsync so client can retry
        throw new NotImplementedException();
    }
}

public sealed class IdempotencyConflictException : Exception
{
    public string IdempotencyKey { get; }

    public IdempotencyConflictException(string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' was already used with a different request payload.")
    {
        IdempotencyKey = idempotencyKey;
    }
}

public sealed class IdempotencyInProgressException : Exception
{
    public string IdempotencyKey { get; }

    public IdempotencyInProgressException(string idempotencyKey)
        : base($"A request with idempotency key '{idempotencyKey}' is currently being processed.")
    {
        IdempotencyKey = idempotencyKey;
    }
}
