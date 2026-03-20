using Microsoft.Extensions.Options;
using PaymentApi.Configuration;

namespace PaymentApi.Services;

/// <summary>
/// Background service that periodically removes expired idempotency records.
/// Runs every half-retention-period to ensure timely cleanup without excessive load.
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(_options.RetentionHours / 2.0);

        _logger.LogInformation(
            "Idempotency cleanup service started. Interval: {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var idempotencyService = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
                await idempotencyService.CleanupExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — expected.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during idempotency cleanup");
            }
        }
    }
}
