namespace PaymentApi.Configuration;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
}
