using System.ComponentModel.DataAnnotations;

namespace InventorySync.Worker.Configuration;

/// <summary>
/// Strongly-typed configuration replacing ConfigurationManager.AppSettings["ApiUrl"].
/// Validated at startup to fail fast on misconfiguration.
/// </summary>
public sealed class InventoryApiOptions
{
    public const string SectionName = "InventoryApi";

    [Required(ErrorMessage = "InventoryApi:BaseUrl is required. Set via appsettings, environment variable, or dotnet user-secrets.")]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
