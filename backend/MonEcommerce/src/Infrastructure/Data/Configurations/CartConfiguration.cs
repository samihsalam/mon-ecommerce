using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id);
        builder.Property(c => c.SessionId).HasMaxLength(200);
        builder.HasIndex(c => c.UserId).HasDatabaseName("ix_carts_user_id");
        builder.HasIndex(c => c.SessionId).HasDatabaseName("ix_carts_session_id");
    }
}
