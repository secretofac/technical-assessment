namespace PaymentApi.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>How long idempotency records are retained before cleanup. Default: 24 hours.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>Maximum allowed length for an idempotency key.</summary>
    public int MaxKeyLength { get; set; } = 128;
}
