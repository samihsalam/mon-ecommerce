using Microsoft.EntityFrameworkCore.ChangeTracking;
using MonEcommerce.Domain.Entities;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Address> Addresses { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PaymentAuditLog> PaymentAuditLogs { get; }
    DbSet<Return> Returns { get; }
    DbSet<EmailDispatchLog> EmailDispatchLogs { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
