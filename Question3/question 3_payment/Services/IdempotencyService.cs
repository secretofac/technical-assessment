using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentApi.Configuration;
using PaymentApi.Data;

namespace PaymentApi.Services;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly IDbContextFactory<PaymentDbContext> _dbContextFactory;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IDbContextFactory<PaymentDbContext> dbContextFactory,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<IdempotencyRecord?> TryClaimKeyAsync(
        string idempotencyKey,
        string merchantId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        // TODO: Implementation steps:
        //   1. Check if record exists for (idempotencyKey, merchantId)
        //   2. If exists → return existing record
        //   3. If not → INSERT new record with Status = "Processing"
        //   4. Catch DbUpdateException (unique constraint violation from concurrent insert)
        //      → fetch and return the record the other instance inserted
        throw new NotImplementedException();
    }

    public Task CompleteAsync(
        string idempotencyKey,
        string merchantId,
        string status,
        string responseBody,
        int httpStatusCode,
        CancellationToken cancellationToken)
    {
        // TODO: Update the existing record with:
        //   - Status = Succeeded or Failed
        //   - ResponseBody = serialised PaymentResponse
        //   - HttpStatusCode
        //   - UpdatedAtUtc
        throw new NotImplementedException();
    }

    public Task ReleaseAsync(
        string idempotencyKey,
        string merchantId,
        CancellationToken cancellationToken)
    {
        // TODO: Delete the record if Status == "Processing"
        //   Allows client to retry after a server-side failure.
        throw new NotImplementedException();
    }

    public Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        // TODO: Delete records where CreatedAtUtc < (now - RetentionHours)
        throw new NotImplementedException();
    }
}

public static class IdempotencyStatus
{
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
