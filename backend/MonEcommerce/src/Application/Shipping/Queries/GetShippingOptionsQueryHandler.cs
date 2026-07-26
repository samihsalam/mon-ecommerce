using MonEcommerce.Application.Shipping.Models;

namespace MonEcommerce.Application.Shipping.Queries;

public class GetShippingOptionsQueryHandler : IRequestHandler<GetShippingOptionsQuery, IReadOnlyList<ShippingOptionDto>>
{
    public Task<IReadOnlyList<ShippingOptionDto>> Handle(GetShippingOptionsQuery request, CancellationToken cancellationToken)
        => Task.FromResult(ShippingOptionsCatalog.Options);
}
