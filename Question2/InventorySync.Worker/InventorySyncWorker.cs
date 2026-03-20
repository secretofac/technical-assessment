using InventorySync.Worker.Services;

namespace InventorySync.Worker;

/// <summary>
/// BackgroundService that runs the inventory sync on a schedule.
///
/// Replaces the Windows Task Scheduler + Console App pattern:
/// - The Generic Host manages lifecycle (start, stop, graceful shutdown).
/// - CancellationToken is provided by the host and honoured throughout.
/// - In Kubernetes, this responds to SIGTERM for clean pod termination.
/// - The periodic timer replaces the external Task Scheduler dependency.
///
/// For one-shot execution (e.g., as a Kubernetes CronJob or Cloud Function),
/// remove the periodic loop and let the service exit after a single run.
/// </summary>
public sealed class InventorySyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<InventorySyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Inventory sync worker started");

        // Use PeriodicTimer for non-drifting intervals without Thread.Sleep
        using var timer = new PeriodicTimer(SyncInterval);

        // Run immediately on startup, then on interval
        do
        {
            try
            {
                // Create a scope for each execution so scoped services
                // (like DbContext if added later) are properly disposed
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IInventorySyncProcessor>();

                await processor.ProcessAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log and continue — a single failed sync should not kill the worker.
                // OperationCanceledException is expected during shutdown and is not logged as error.
                logger.LogError(ex, "Inventory sync failed. Will retry at next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        logger.LogInformation("Inventory sync worker stopping");
    }
}
