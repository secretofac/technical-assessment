namespace PaymentApi.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Default requests per window for merchants without a specific override.</summary>
    public int DefaultPermitLimit { get; set; } = 100;

    /// <summary>Sliding window size in seconds.</summary>
    public int WindowSizeSeconds { get; set; } = 60;

    /// <summary>Number of segments in the sliding window. More segments = smoother rate limiting.</summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Per-merchant overrides. Key = MerchantId, Value = permit limit for that merchant.</summary>
    public Dictionary<string, int> MerchantOverrides { get; set; } = new();
}
