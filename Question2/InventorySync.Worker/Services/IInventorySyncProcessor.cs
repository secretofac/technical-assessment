namespace InventorySync.Worker.Services;

/// <summary>
/// Abstraction for inventory sync processing logic.
/// </summary>
public interface IInventorySyncProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken);
}
