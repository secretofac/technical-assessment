using InventorySync.Worker;
using InventorySync.Worker.Configuration;
using InventorySync.Worker.Services;

// ──────────────────────────────────────────────────────────────
// ARCHITECTURAL JUSTIFICATION:
//
// 1. Worker Service over Console App:
//    The original was a console app on Windows Task Scheduler.
//    A .NET Worker Service integrates with the Generic Host, giving us:
//    - Dependency Injection (IServiceCollection)
//    - Configuration (IConfiguration with appsettings, env vars, secrets)
//    - Structured Logging (ILogger<T>)
//    - Graceful shutdown via CancellationToken
//    - Native Docker/Kubernetes readiness (responds to SIGTERM)
//
// 2. IHttpClientFactory over WebClient:
//    WebClient is obsolete since .NET 6. HttpClient created via
//    IHttpClientFactory handles socket pooling, DNS rotation, and
//    integrates with Polly for resilience.
//
// 3. System.Text.Json over Newtonsoft.Json:
//    System.Text.Json is the built-in, high-performance JSON library
//    in .NET. It supports source generators for AOT and zero-allocation
//    reads via Utf8JsonReader. Newtonsoft is unnecessary for new code.
//
// 4. IOptions<T> over ConfigurationManager:
//    ConfigurationManager is .NET Framework legacy. IOptions<T> provides
//    strongly-typed, validated, reloadable configuration with DI support.
//
// 5. ILogger<T> over Console.WriteLine / File.AppendAllText:
//    Structured logging captures key-value pairs (not just strings),
//    enabling filtering, searching, and alerting in tools like
//    Seq, Application Insights, or ELK. Console output in containers
//    is captured by the orchestrator's log driver automatically.
//
// 6. Strongly-typed DTOs over DataTable:
//    DataTable is untyped, heavy (boxing, extra metadata), and
//    was designed for ADO.NET DataSets — not for JSON deserialization.
//    A POCO record is type-safe, allocation-efficient, and works
//    natively with System.Text.Json.
// ──────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration section to a strongly-typed options class
// Value comes from appsettings.json, environment variables, or dotnet user-secrets
builder.Services.Configure<InventoryApiOptions>(
    builder.Configuration.GetSection(InventoryApiOptions.SectionName));

// Register HttpClient with base address from config and resilience pipeline
builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>((serviceProvider, client) =>
{
    var options = builder.Configuration
        .GetSection(InventoryApiOptions.SectionName)
        .Get<InventoryApiOptions>()
        ?? throw new InvalidOperationException(
            $"Configuration section '{InventoryApiOptions.SectionName}' is missing or invalid.");

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(); // Polly: retry + circuit breaker + timeout

// Register the Worker as a hosted service
builder.Services.AddScoped<IInventorySyncProcessor, InventorySyncProcessor>();
builder.Services.AddHostedService<InventorySyncWorker>();

var host = builder.Build();
host.Run();
