using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Issues found in the original OrderProcessor:
//
// 1. async void return type          - exceptions are unobservable, caller can't await
// 2. Full table loaded into memory   - ToListAsync() with no filter pulls every row
// 3. Parallel.ForEach + async lambda - ForEach returns before async work finishes
// 4. new HttpClient() per call       - sockets linger in TIME_WAIT, causes exhaustion
// 5. .Result blocking call           - synchronously blocks a thread, deadlocks in some contexts
// 6. DbContext shared across threads - DbContext is not thread-safe
// 7. catch(Exception) + Console      - swallows errors silently, nothing useful logged
// 8. Magic strings for status        - typo like "Proccessed" compiles fine, breaks silently
// 9. No CancellationToken            - can't cancel on shutdown, leaks in-flight work
// 10. No HTTP resilience             - single transient failure kills the whole batch
// 11. Hand-built JSON string         - single quotes are invalid JSON, no escaping

// Fix for #8: enum gives compile-time safety instead of raw "Pending"/"Processed" strings
public enum OrderStatus
{
    Pending,
    Processed,
    Failed
}

public sealed class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

// Fix for #11: a typed record serialised by System.Text.Json instead of $"{{ 'orderId': {id} }}"
// The original used single quotes which is invalid JSON, and had no escaping or type safety.
internal sealed record LogisticsShipmentRequest(int OrderId);

public sealed class OrderProcessingService
{
    // Fix for #6: IDbContextFactory creates a fresh, independent context per operation.
    // The original injected a single shared DbContext into Parallel.ForEach — DbContext
    // is not thread-safe, so concurrent access causes data corruption or InvalidOperationException.
    //
    // Fix for #4: HttpClient is injected via IHttpClientFactory, not created with new HttpClient().
    // Each new HttpClient() allocates a socket. Disposing it doesn't release the socket immediately —
    // it sits in TIME_WAIT for up to 240s. Under any real load this causes SocketException.
    //
    // Fix for #7: ILogger<T> replaces Console.WriteLine. Console output isn't structured,
    // isn't searchable, and is often swallowed entirely in containers. Structured logging
    // lets you query by OrderId, correlate traces, and set alert thresholds.
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderProcessingService> _logger;

    private const int MaxConcurrency = 5;

    public OrderProcessingService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        HttpClient httpClient,
        ILogger<OrderProcessingService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpClient = httpClient;
        _logger = logger;
    }

    // Fix for #1: returns Task instead of void.
    // async void means the caller can't await the method, so it has no way to know when
    // processing finishes or whether it succeeded. Any exception thrown inside async void
    // can't be caught by the caller — it surfaces as an unhandled exception and crashes the process.
    //
    // Fix for #9: CancellationToken added throughout.
    // Without it, when a container receives SIGTERM all pending HTTP calls and DB queries
    // keep running past the shutdown deadline — no graceful stop is possible.
    public async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        List<Order> pendingOrders;

        await using (var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Fix for #2: the WHERE clause runs in SQL, not in application memory.
            // The original called ToListAsync() first with no filter, then filtered with
            // LINQ-to-Objects. That pulls every row across the wire regardless of status —
            // with millions of orders and only a handful pending, that's unbounded memory usage.
            pendingOrders = await readContext.Orders
                .Where(o => o.Status == OrderStatus.Pending)
                .ToListAsync(cancellationToken);
        }

        if (pendingOrders.Count == 0)
        {
            _logger.LogInformation("No pending orders to process");
            return;
        }

        _logger.LogInformation("Processing {Count} pending orders", pendingOrders.Count);

        // Fix for #3: Parallel.ForEachAsync instead of Parallel.ForEach with an async lambda.
        // Parallel.ForEach expects Action<T>, so an async lambda becomes async void.
        // Two consequences: exceptions are unobserved and crash the process, and ForEach returns
        // immediately before any async work completes. In the original, SaveChangesAsync ran
        // before a single order had been marked "Processed" — all status updates were lost.
        //
        // Fix for #10: resilience is wired at the DI level via AddStandardResilienceHandler().
        // A single transient 502 in the original would fail the whole batch with no retry.
        await Parallel.ForEachAsync(
            pendingOrders,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (order, token) =>
            {
                // Each order gets its own DbContext — safe for parallel use (fix for #6)
                await using var context = await _dbContextFactory.CreateDbContextAsync(token);
                context.Orders.Attach(order);

                try
                {
                    _logger.LogInformation("Notifying logistics for Order {OrderId}", order.Id);

                    // Fix for #5: await instead of .Result.
                    // The original called ReadAsStringAsync().Result, blocking the thread synchronously.
                    // In any context with a SynchronizationContext this deadlocks — the async
                    // continuation can't resume because the thread is blocked waiting for it.
                    //
                    // Fix for #4 + #11: injected HttpClient, PostAsJsonAsync with a typed DTO.
                    var response = await _httpClient.PostAsJsonAsync(
                        "ship",
                        new LogisticsShipmentRequest(order.Id),
                        token);

                    response.EnsureSuccessStatusCode();
                    order.Status = OrderStatus.Processed;
                }
                catch (HttpRequestException ex)
                {
                    // Targeted catch for HTTP failures only — not a bare catch(Exception).
                    // The original swallowed everything silently into Console.WriteLine (fix for #7).
                    // Isolating the failure per order means one bad call doesn't abort the batch.
                    _logger.LogError(ex,
                        "Failed to notify logistics for Order {OrderId}. Marking as Failed",
                        order.Id);
                    order.Status = OrderStatus.Failed;
                }

                await context.SaveChangesAsync(token);
            });

        _logger.LogInformation("Completed processing pending orders");
    }
}
