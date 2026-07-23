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

        // Unique (not just indexed) and filtered to non-null values — CartService's find-or-create
        // is a check-then-act with two separate round trips (no transaction), so without a DB-level
        // constraint, two concurrent requests for the same brand-new owner (e.g. a double-click
        // "add to cart", or two tabs sharing a session id) could each observe "no cart exists" and
        // both insert one, silently splitting the visitor's cart across two rows. The filtered
        // WHERE clause is required because Cart rows always have exactly one of UserId/SessionId
        // null (CartOwner's own invariant) — a plain unique index on either column would reject
        // every row after the first, since SQL Server treats multiple NULLs as satisfying
        // uniqueness only when ALSO filtered (the filter is what makes "many rows with NULL
        // UserId" legal while still enforcing "at most one row per non-null UserId").
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL").HasDatabaseName("ix_carts_user_id");
        builder.HasIndex(c => c.SessionId).IsUnique().HasFilter("[SessionId] IS NOT NULL").HasDatabaseName("ix_carts_session_id");
    }
}
