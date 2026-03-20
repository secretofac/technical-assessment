namespace PaymentApi.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int DefaultPermitLimit { get; set; }
    public int WindowSizeSeconds { get; set; }
    public int SegmentsPerWindow { get; set; }
    public Dictionary<string, int> MerchantOverrides { get; set; } = new();
}
