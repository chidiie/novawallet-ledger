using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Auditing;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.WalletId)
            .HasColumnName("wallet_id")
            .HasConversion(id => id.Value, value => WalletId.From(value));

        builder.Property(a => a.EventType).HasColumnName("event_type").HasConversion<int>();

        builder.Property(a => a.Amount)
            .HasColumnName("amount_kobo").HasConversion(m => m.Kobo, k => Money.FromKobo(k));

        builder.Property(a => a.BalanceBefore)
            .HasColumnName("balance_before_kobo").HasConversion(m => m.Kobo, k => Money.FromKobo(k));

        builder.Property(a => a.BalanceAfter)
            .HasColumnName("balance_after_kobo").HasConversion(m => m.Kobo, k => Money.FromKobo(k));

        builder.Property(a => a.Actor).HasColumnName("actor").HasMaxLength(64).IsRequired();

        builder.Property(a => a.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(64).IsRequired();

        builder.Property(a => a.Reference)
            .HasColumnName("reference").HasMaxLength(64).IsRequired();

        builder.Property(a => a.OccurredAtUtc).HasColumnName("occurred_at_utc");

        builder.HasIndex(a => new { a.WalletId, a.OccurredAtUtc })
            .HasDatabaseName("ix_audit_logs_wallet_occurred");

        builder.HasIndex(a => a.CorrelationId).HasDatabaseName("ix_audit_logs_correlation_id");
    }
}