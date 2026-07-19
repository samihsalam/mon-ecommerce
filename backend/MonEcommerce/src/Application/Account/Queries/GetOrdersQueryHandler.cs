using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Account.Queries;

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, PagedResult<OrderSummaryDto>>
{
    private readonly IAccountService _accountService;
    private readonly IUser _user;

    public GetOrdersQueryHandler(IAccountService accountService, IUser user)
    {
        _accountService = accountService;
        _user = user;
    }

    public Task<PagedResult<OrderSummaryDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
        => _accountService.GetOrdersAsync(_user.Id!, request.Page, request.PageSize, cancellationToken);
}
