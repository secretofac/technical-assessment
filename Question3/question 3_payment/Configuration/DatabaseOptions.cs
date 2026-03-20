namespace PaymentApi.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public int MaxPoolSize { get; set; }
    public int CommandTimeoutSeconds { get; set; }
}
