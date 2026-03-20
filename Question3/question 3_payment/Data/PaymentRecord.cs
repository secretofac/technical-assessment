namespace PaymentApi.Data;

/// <summary>
/// Entity for persisted payment transactions.
/// </summary>
public sealed class PaymentRecord
{
    public Guid Id { get; set; }
    public required string MerchantId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string PaymentMethodToken { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? FailureReason { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
