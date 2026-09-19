using Microsoft.EntityFrameworkCore;
using NovaWallet.Domain.Auditing;
using NovaWallet.Domain.Ledger;
using NovaWallet.Domain.Wallets;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence;

public sealed class NovaWalletDbContext : DbContext
{
    public NovaWalletDbContext(DbContextOptions<NovaWalletDbContext> options)
        : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<DailyTransferTotal> DailyTransferTotals => Set<DailyTransferTotal>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    /// <summary>
    /// Audit entries. Exposed so they can be INSERTed, and for nothing else —
    /// the append-only rule is enforced in SaveChanges below, and again by the
    /// database grants in the migration.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NovaWalletDbContext).Assembly);
    }

    /// <summary>
    /// Last line of defence for the append-only audit trail: if any code path
    /// ever tries to modify or delete an audit row, the save fails loudly rather
    /// than quietly rewriting history.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tampered = ChangeTracker.Entries<AuditLogEntry>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (tampered)
        {
            throw new InvalidOperationException(
                "The audit log is append-only. Audit entries cannot be modified or deleted.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}