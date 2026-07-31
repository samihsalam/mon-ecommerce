using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class EmailDispatchLogConfiguration : IEntityTypeConfiguration<EmailDispatchLog>
{
    public void Configure(EntityTypeBuilder<EmailDispatchLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Recipient).IsRequired().HasMaxLength(320); // RFC 5321 max email length
        builder.Property(e => e.SendGridMessageId).HasMaxLength(255);
        builder.HasIndex(e => e.EventType).HasDatabaseName("ix_email_dispatch_logs_event_type");
    }
}
