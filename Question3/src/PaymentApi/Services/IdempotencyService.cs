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

    public async Task<IdempotencyRecord?> TryClaimKeyAsync(
        string idempotencyKey,
        string merchantId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // First, check if a record already exists for this key+merchant.
        var existing = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.IdempotencyKey == idempotencyKey && r.MerchantId == merchantId,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        // No existing record — attempt to insert a new one with status "Processing".
        // The unique index on (IdempotencyKey, MerchantId) guarantees that only ONE
        // concurrent insert will succeed. The loser gets a DbUpdateException.
        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            MerchantId = merchantId,
            RequestHash = requestHash,
            Status = IdempotencyStatus.Processing,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.IdempotencyRecords.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Idempotency key claimed: {IdempotencyKey} for merchant {MerchantId}",
                idempotencyKey, merchantId);

            // null signals "key successfully claimed — proceed with payment"
            return null;
        }
        catch (DbUpdateException)
        {
            // Another instance won the race. Fetch the record that was inserted by the winner.
            _logger.LogInformation(
                "Idempotency key already claimed by concurrent request: {IdempotencyKey}",
                idempotencyKey);

            await using var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await readContext.IdempotencyRecords
                .AsNoTracking()
                .FirstAsync(
                    r => r.IdempotencyKey == idempotencyKey && r.MerchantId == merchantId,
                    cancellationToken);
        }
    }

    public async Task CompleteAsync(
        string idempotencyKey,
        string merchantId,
        string status,
        string responseBody,
        int httpStatusCode,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await dbContext.IdempotencyRecords
            .FirstAsync(
                r => r.IdempotencyKey == idempotencyKey && r.MerchantId == merchantId,
                cancellationToken);

        record.Status = status;
        record.ResponseBody = responseBody;
        record.HttpStatusCode = httpStatusCode;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Idempotency key completed: {IdempotencyKey}, status {Status}",
            idempotencyKey, status);
    }

    public async Task ReleaseAsync(
        string idempotencyKey,
        string merchantId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(
                r => r.IdempotencyKey == idempotencyKey
                     && r.MerchantId == merchantId
                     && r.Status == IdempotencyStatus.Processing,
                cancellationToken);

        if (record is not null)
        {
            dbContext.IdempotencyRecords.Remove(record);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Idempotency key released (processing failed): {IdempotencyKey}",
                idempotencyKey);
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateTimeOffset.UtcNow.AddHours(-_options.RetentionHours);

        var deleted = await dbContext.IdempotencyRecords
            .Where(r => r.CreatedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired idempotency records", deleted);
        }
    }
}

/// <summary>Named constants to avoid magic strings for idempotency status.</summary>
public static class IdempotencyStatus
{
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
