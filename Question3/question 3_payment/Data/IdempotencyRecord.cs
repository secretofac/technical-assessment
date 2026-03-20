namespace PaymentApi.Data;

/// <summary>
/// Entity for idempotency state persistence.
/// Unique constraint on (IdempotencyKey, MerchantId) prevents double-processing.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string MerchantId { get; set; }
    public required string RequestHash { get; set; }
    public required string Status { get; set; }
    public string? ResponseBody { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
