using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Domain.Entities;

public class Return : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ReturnReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = [];
    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;
}
