using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration
    : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        // The key IS the primary key. That primary-key constraint is what makes
        // insert-first duplicate detection work — a concurrent second insert of
        // the same key fails at the database, with no race window.
        builder.HasKey(r => r.Key);

        builder.Property(r => r.Key).HasColumnName("key").HasMaxLength(128);

        builder.Property(r => r.RequestHash)
            .HasColumnName("request_hash").HasMaxLength(64).IsRequired();

        builder.Property(r => r.State).HasColumnName("state").HasConversion<int>();

        builder.Property(r => r.ResponseStatusCode).HasColumnName("response_status_code");

        builder.Property(r => r.ResponseBody).HasColumnName("response_body");

        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.CompletedAtUtc).HasColumnName("completed_at_utc");
    }
}