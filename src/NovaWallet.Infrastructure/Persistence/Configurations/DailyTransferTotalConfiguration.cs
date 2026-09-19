using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Wallets;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public sealed class DailyTransferTotalConfiguration
    : IEntityTypeConfiguration<DailyTransferTotal>
{
    public void Configure(EntityTypeBuilder<DailyTransferTotal> builder)
    {
        builder.ToTable("daily_transfer_totals", t =>
            t.HasCheckConstraint("ck_daily_totals_non_negative", "total_sent_kobo >= 0"));

        // Composite key: one row per wallet per WAT day. This IS the uniqueness
        // guarantee the limit depends on — no separate unique index needed.
        builder.HasKey(d => new { d.WalletId, d.WatDate });

        builder.Property(d => d.WalletId)
            .HasColumnName("wallet_id")
            .HasConversion(id => id.Value, value => WalletId.From(value));

        builder.Property(d => d.WatDate).HasColumnName("wat_date").HasColumnType("date");

        builder.Property(d => d.TotalSentKobo).HasColumnName("total_sent_kobo");
    }
}