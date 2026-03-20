using InventorySync.Worker.Models;

namespace InventorySync.Worker.Services;

/// <summary>
/// Abstraction for the external inventory API.
/// Dependency Inversion: the processor depends on this interface,
/// not on HttpClient or any specific transport.
/// </summary>
public interface IInventoryApiClient
{
    /// <summary>
    /// Fetches the current inventory from the external API.
    /// </summary>
    IAsyncEnumerable<InventoryItem> GetInventoryAsync(CancellationToken cancellationToken);
}
