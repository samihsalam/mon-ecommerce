using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MonEcommerce.Domain.Entities;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id);
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.ProcessedByAdminUserId);

        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_account_deletion_requests_user_id");
        builder.HasIndex(r => r.Status).HasDatabaseName("ix_account_deletion_requests_status");
    }
}
