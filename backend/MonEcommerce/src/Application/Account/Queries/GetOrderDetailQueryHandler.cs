using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Account.Queries;

public class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, OrderDetailDto>
{
    private readonly IAccountService _accountService;
    private readonly IUser _user;

    public GetOrderDetailQueryHandler(IAccountService accountService, IUser user)
    {
        _accountService = accountService;
        _user = user;
    }

    public Task<OrderDetailDto> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
        => _accountService.GetOrderDetailAsync(_user.Id!, request.OrderId, cancellationToken);
}
