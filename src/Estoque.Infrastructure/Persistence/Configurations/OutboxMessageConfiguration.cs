using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<Outbox.OutboxMessage>
{
    public void Configure(EntityTypeBuilder<Outbox.OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");

        // Fila do dispatcher: mensagens pendentes primeiro.
        builder.HasIndex(x => new { x.PublishedAtUtc })
            .HasDatabaseName("IX_outbox_messages_pending");
    }
}
