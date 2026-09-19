using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets", t =>
            // THE BACKSTOP. Even if every line of C# above it were wrong, the
            // database physically refuses to store a negative balance.
            t.HasCheckConstraint("ck_wallets_balance_non_negative", "balance_kobo >= 0"));

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => WalletId.From(value));

        builder.Property(w => w.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(w => w.Balance)
            .HasColumnName("balance_kobo")
            .HasColumnType("bigint")
            .HasConversion(money => money.Kobo, kobo => Money.FromKobo(kobo))
            .IsRequired();

        builder.Property(w => w.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(w => w.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(w => w.CustomerId).HasDatabaseName("ix_wallets_customer_id");
    }
}