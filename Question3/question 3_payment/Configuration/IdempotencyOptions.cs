namespace PaymentApi.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public int RetentionHours { get; set; }
    public int MaxKeyLength { get; set; }
}
