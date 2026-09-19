using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Ledger;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.WalletId)
            .HasColumnName("wallet_id")
            .HasConversion(id => id.Value, value => WalletId.From(value));

        builder.Property(e => e.Direction)
            .HasColumnName("direction")
            .HasConversion<int>();

        builder.Property(e => e.Amount)
            .HasColumnName("amount_kobo")
            .HasConversion(m => m.Kobo, k => Money.FromKobo(k));

        builder.Property(e => e.BalanceAfter)
            .HasColumnName("balance_after_kobo")
            .HasConversion(m => m.Kobo, k => Money.FromKobo(k));

        builder.Property(e => e.Reference)
            .HasColumnName("reference").HasMaxLength(64).IsRequired();

        builder.Property(e => e.Narration)
            .HasColumnName("narration").HasMaxLength(140);

        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc");

        // Covers the statement query exactly: filter by wallet, order newest first.
        builder.HasIndex(e => new { e.WalletId, e.OccurredAtUtc })
            .HasDatabaseName("ix_ledger_entries_wallet_occurred")
            .IsDescending(false, true);

        builder.HasIndex(e => e.Reference).HasDatabaseName("ix_ledger_entries_reference");

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}