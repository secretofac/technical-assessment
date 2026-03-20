namespace PaymentApi.Data;

/// <summary>
/// Represents a stored idempotency entry.
/// The unique constraint on (IdempotencyKey, MerchantId) guarantees that
/// only one row can exist per key-merchant pair, preventing double-charge
/// even under concurrent inserts across multiple API instances.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }

    /// <summary>Client-supplied idempotency key (validated server-side).</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>Merchant that owns this request — scoped to prevent cross-merchant key collisions.</summary>
    public required string MerchantId { get; set; }

    /// <summary>SHA-256 hash of the request payload. Used to reject retries with different payloads.</summary>
    public required string RequestHash { get; set; }

    /// <summary>Processing / Succeeded / Failed</summary>
    public required string Status { get; set; }

    /// <summary>Serialised PaymentResponse returned to the client. Null while still Processing.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>HTTP status code to return for cached responses.</summary>
    public int? HttpStatusCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
