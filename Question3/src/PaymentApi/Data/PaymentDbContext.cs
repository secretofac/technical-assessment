using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Data;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Critical: unique constraint prevents two concurrent inserts for the same key+merchant.
            // The first INSERT wins; the second throws a DbUpdateException, which the service catches.
            entity.HasIndex(e => new { e.IdempotencyKey, e.MerchantId })
                  .IsUnique()
                  .HasDatabaseName("IX_Idempotency_Key_Merchant");

            entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(e => e.MerchantId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(16).IsRequired();
            entity.Property(e => e.ResponseBody).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MerchantId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.PaymentMethodToken).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(16).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();

            entity.HasIndex(e => e.MerchantId).HasDatabaseName("IX_Payment_MerchantId");
            entity.HasIndex(e => e.IdempotencyKey).HasDatabaseName("IX_Payment_IdempotencyKey");
        });
    }
}
