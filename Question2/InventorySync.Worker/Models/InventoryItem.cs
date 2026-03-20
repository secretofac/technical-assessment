using System.Text.Json.Serialization;

namespace InventorySync.Worker.Models;

/// <summary>
/// Typed DTO replacing DataTable/DataRow.
///
/// Why a record:
/// - Immutable by default — inventory data from the API is read-only in this context.
/// - Value-based equality for free — useful for deduplication or testing.
/// - Compact syntax for what is essentially a data carrier.
///
/// JsonPropertyName attributes ensure correct mapping regardless of
/// casing conventions in the external API response.
/// </summary>
public sealed record InventoryItem(
    [property: JsonPropertyName("SKU")] string Sku,
    [property: JsonPropertyName("Price")] decimal Price);
