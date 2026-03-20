using Microsoft.Extensions.Logging;

namespace InventorySync.Worker.Services;

/// <summary>
/// Core sync logic — fetches inventory and processes each item.
///
/// Replaces the original InventorySyncer.Run() with:
/// - Structured logging instead of File.AppendAllText to a hardcoded path
/// - Async streaming instead of synchronous blocking
/// - CancellationToken for graceful shutdown
///
/// Why structured logging over file writes:
/// The original wrote to "C:\Logs\daily_sync.txt" — a hardcoded Windows path
/// that doesn't exist in containers, has no rotation, no searchability,
/// and fails silently if the directory is missing. Structured logging via
/// ILogger<T> outputs to stdout (captured by Docker/K8s log drivers) and
/// can be routed to any sink (Seq, Application Insights, ELK) via configuration.
/// </summary>
public sealed class InventorySyncProcessor(
    IInventoryApiClient apiClient,
    ILogger<InventorySyncProcessor> logger) : IInventorySyncProcessor
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var itemCount = 0;

        await foreach (var item in apiClient.GetInventoryAsync(cancellationToken))
        {
            // Structured log — SKU and Price are captured as queryable fields,
            // not interpolated into a flat string like the original format()
            logger.LogInformation(
                "Processed Item: {Sku} - Price: {Price:C}",
                item.Sku,
                item.Price);

            itemCount++;
        }

        logger.LogInformation("Sync completed. Total items processed: {ItemCount}", itemCount);
    }
}
