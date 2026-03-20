using Microsoft.Extensions.Options;
using PaymentApi.Configuration;

namespace PaymentApi.Services;

/// <summary>
/// Background service that periodically removes expired idempotency records.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: Implementation steps:
        //   1. Loop while not cancelled
        //   2. Delay for (RetentionHours / 2) interval
        //   3. Resolve IIdempotencyService from a new scope
        //   4. Call CleanupExpiredAsync
        //   5. Catch and log errors (do not crash the host)
        throw new NotImplementedException();
    }
}
