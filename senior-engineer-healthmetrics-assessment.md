# Technical Assessment

## Question 1

Review the following C# class (`OrderProcessor`)

1. Identify all critical issues related to performance, scalability and stability
2. Refactor the code to fix these issues using modern .NET 10 best practices
3. You must explain why the original approach was dangerous

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class OrderProcessor(AppDbContext dbContext)
{
    /// <summary>
    /// Process all pending orders
    /// </summary>
    public async void ProcessPendingOrders()
    {
        try
        {
            // Fetch all orders to filter in memory to avoid DB logic complexity
            var orders = await dbContext.Orders.ToListAsync();
            var pendingOrders = orders.Where(o => o.Status == "Pending").ToList();

            // Process each order in parallel for speed
            Parallel.ForEach(pendingOrders, async order =>
            {
                await NotifyExternalLogistics(order);
                order.Status = "Processed";
            });

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing orders: {ex.Message}");
        }
    }

    /// <summary>
    /// Notify external API
    /// </summary>
    private async Task NotifyExternalLogistics(Order order)
    {
        // Create a new client for a fresh connection ensuring no stale DNS
        using (var client = new HttpClient())
        {
            var payload = new StringContent($"{{ 'orderId': {order.Id} }}", System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.logistics.com/ship", payload);

            // Block here to ensure we get the result before moving on
            var result = response.Content.ReadAsStringAsync().Result;
        }
    }
}
```

---

## Question 2

The following is a legacy background service written in .NET Framework 4.5 years ago. It runs as a Windows Console App on a scheduled task.

Your goal is to modernise this logic to run as a .NET 10 Worker Service (or Cloud Function) that can be deployed to a container (Docker).

You must rewrite the core logic. Justify your architectural choices in comments.

**The Requirements:**

1. Replace obsolete libraries with their modern .NET 10 equivalents.
2. Implement proper Dependency Injection.
3. Switch to Structured Logging (instead of Console).
4. Ensure the code is Asynchronous all the way down.
5. Optimise for Memory & JSON performance.

```csharp
using System;
using System.Data;
using System.Net;
using System.IO;
using System.Configuration;
using Newtonsoft.Json;

namespace LegacyInventorySync
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Sync...");
            var sync = new InventorySyncer();
            sync.Run();
            Console.WriteLine("Sync Complete.");
        }
    }

    public class InventorySyncer
    {
        public void Run()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string url = ConfigurationManager.AppSettings["ApiUrl"];
                    string json = client.DownloadString(url);

                    DataTable dt = JsonConvert.DeserializeObject<DataTable>(json);

                    foreach (DataRow row in dt.Rows)
                    {
                        string logLine = string.Format("Processed Item: {0} - Price: {1}", row["SKU"], row["Price"]);
                        File.AppendAllText("C:\\Logs\\daily_sync.txt", logLine + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
```

---

## Question 3

You are the lead developer for a high-volume **Payment Processing API**. We are experiencing two critical issues:

1. **Double charging:** when a client retries a timeout request, we may end up processing the payment twice.
2. **Abuse:** a specific merchant is flooding our API, crashing the database.

You must design and provide a .NET 10 minimal API solution with a payment API that addresses the problems above. If you use generative AI to assist in your work, you must provide the **exact prompts** you gave AI, alongside the final code structure. We are grading your prompts as much as the code.
