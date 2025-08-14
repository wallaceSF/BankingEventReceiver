using BankingApi.EventReceiver.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.EventReceiver.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<BankAccount>(e =>
        {
            e.ToTable("BankAccounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        });

        b.Entity<ProcessedMessage>(e =>
        {
            e.ToTable("ProcessedMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProcessedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
