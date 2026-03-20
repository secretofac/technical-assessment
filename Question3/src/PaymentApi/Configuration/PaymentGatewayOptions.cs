namespace PaymentApi.Configuration;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    /// <summary>Base URL for the external payment gateway.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Timeout in seconds for gateway calls.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
