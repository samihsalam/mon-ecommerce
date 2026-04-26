using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id);
        builder.Property(a => a.Street).IsRequired().HasMaxLength(500);
        builder.Property(a => a.City).IsRequired().HasMaxLength(200);
        builder.Property(a => a.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Country).IsRequired().HasMaxLength(100);
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_addresses_user_id");
    }
}
