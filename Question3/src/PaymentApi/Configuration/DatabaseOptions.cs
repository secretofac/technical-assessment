namespace PaymentApi.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Maximum connections in the pool. Prevents a single service from exhausting the DB.</summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>Command timeout in seconds. Prevents long-running queries from holding connections.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
