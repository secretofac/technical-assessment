using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    public async Task<(PaymentResponse Response, int HttpStatusCode)> ProcessPaymentAsync(
        string merchantId,
        string idempotencyKey,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        var requestHash = ComputeRequestHash(request);

        // Step 1: Attempt to claim the idempotency key.
        var existingRecord = await _idempotencyService.TryClaimKeyAsync(
            idempotencyKey, merchantId, requestHash, cancellationToken);

        // Key already exists — handle each scenario.
        if (existingRecord is not null)
        {
            return HandleExistingRecord(existingRecord, requestHash, idempotencyKey);
        }

        // Step 2: Key claimed successfully — we are the first (or only) processor.
        // Call the external gateway OUTSIDE any database transaction to avoid
        // long-running locks and connection pool exhaustion.
        try
        {
            var gatewayResult = await _paymentGateway.ChargeAsync(
                merchantId,
                request.Amount,
                request.Currency,
                request.PaymentMethodToken,
                idempotencyKey,
                cancellationToken);

            // Step 3: Persist the payment result.
            var transactionId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var status = gatewayResult.Succeeded ? "Succeeded" : "Failed";

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.PaymentRecords.Add(new PaymentRecord
            {
                Id = transactionId,
                MerchantId = merchantId,
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethodToken = request.PaymentMethodToken,
                Description = request.Description,
                Status = status,
                GatewayTransactionId = gatewayResult.GatewayTransactionId,
                FailureReason = gatewayResult.FailureReason,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new PaymentResponse(
                TransactionId: transactionId,
                Status: status,
                Amount: request.Amount,
                Currency: request.Currency,
                Timestamp: now);

            var httpStatusCode = gatewayResult.Succeeded
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK;

            // Step 4: Mark idempotency key as completed with the cached response.
            var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
            var idempotencyStatus = gatewayResult.Succeeded
                ? IdempotencyStatus.Succeeded
                : IdempotencyStatus.Failed;

            await _idempotencyService.CompleteAsync(
                idempotencyKey, merchantId, idempotencyStatus,
                responseJson, httpStatusCode, cancellationToken);

            _logger.LogInformation(
                "Payment processed: TransactionId={TransactionId}, Merchant={MerchantId}, Status={Status}",
                transactionId, merchantId, status);

            return (response, httpStatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Gateway call or persistence failed — release the idempotency key
            // so the client can retry. This handles the case where the gateway
            // call itself threw (network error, timeout) or the DB save after
            // a successful gateway call failed (crash-before-persist scenario).
            //
            // IMPORTANT: If the gateway succeeded but we crash here, the next
            // retry will re-call the gateway. The gateway's own idempotency key
            // (which we pass through) ensures no double-charge at the gateway level.
            _logger.LogError(ex,
                "Payment processing failed for idempotency key {IdempotencyKey}. Releasing key for retry.",
                idempotencyKey);

            await _idempotencyService.ReleaseAsync(idempotencyKey, merchantId, CancellationToken.None);
            throw;
        }
    }

    private (PaymentResponse Response, int HttpStatusCode) HandleExistingRecord(
        IdempotencyRecord existing, string requestHash, string idempotencyKey)
    {
        // Scenario (d): Same key, different payload — reject.
        if (existing.RequestHash != requestHash)
        {
            _logger.LogWarning(
                "Idempotency key reuse with different payload: {IdempotencyKey}",
                idempotencyKey);

            throw new IdempotencyConflictException(idempotencyKey);
        }

        // Scenario (c): First request still processing — tell client to wait.
        if (existing.Status == IdempotencyStatus.Processing)
        {
            _logger.LogInformation(
                "Idempotency key still processing: {IdempotencyKey}",
                idempotencyKey);

            throw new IdempotencyInProgressException(idempotencyKey);
        }

        // Scenario (b): First request completed — return cached response.
        if (existing.ResponseBody is null)
        {
            throw new InvalidOperationException(
                $"Idempotency record for key {idempotencyKey} is completed but has no stored response.");
        }

        var cachedResponse = JsonSerializer.Deserialize<PaymentResponse>(
            existing.ResponseBody, SerializerOptions)!;

        _logger.LogInformation(
            "Returning cached response for idempotency key: {IdempotencyKey}",
            idempotencyKey);

        return (cachedResponse, existing.HttpStatusCode ?? StatusCodes.Status200OK);
    }

    private static string ComputeRequestHash(PaymentRequest request)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hashBytes);
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
