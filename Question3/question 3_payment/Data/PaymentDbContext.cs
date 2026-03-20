using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Data;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: Configure IdempotencyRecord entity
        //   - Unique index on (IdempotencyKey, MerchantId)
        //   - Column types and max lengths
        throw new NotImplementedException();

        // TODO: Configure PaymentRecord entity
        //   - Indexes on MerchantId, IdempotencyKey
        //   - Precision for Amount
        throw new NotImplementedException();
    }
}
