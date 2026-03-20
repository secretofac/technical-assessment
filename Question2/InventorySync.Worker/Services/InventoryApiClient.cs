using System.Runtime.CompilerServices;
using System.Text.Json;
using InventorySync.Worker.Models;
using Microsoft.Extensions.Logging;

namespace InventorySync.Worker.Services;

/// <summary>
/// HTTP client for the external inventory API.
///
/// Key design choices:
///
/// 1. IAsyncEnumerable streaming instead of loading entire response:
///    The original loaded everything into a DataTable in one shot.
///    IAsyncEnumerable + DeserializeAsyncEnumerable streams items one at a time,
///    keeping memory constant regardless of response size. For very large
///    inventory lists this prevents OutOfMemoryException.
///
/// 2. HttpClient injected via IHttpClientFactory (typed client pattern):
///    The factory manages handler pooling and DNS rotation. The typed client
///    pattern gives us a clean, testable interface.
///
/// 3. CancellationToken propagated for graceful shutdown.
/// </summary>
public sealed class InventoryApiClient(
    HttpClient httpClient,
    ILogger<InventoryApiClient> logger) : IInventoryApiClient
{
    public async IAsyncEnumerable<InventoryItem> GetInventoryAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching inventory from external API");

        // Stream the response instead of buffering the entire body
        using var response = await httpClient.GetAsync(
            string.Empty,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // DeserializeAsyncEnumerable reads JSON array items one-at-a-time
        // without buffering the entire collection — O(1) memory usage
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<InventoryItem>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken))
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
