using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Account.Queries;

public class GetOrderByPaymentIntentQueryHandler : IRequestHandler<GetOrderByPaymentIntentQuery, OrderDetailDto>
{
    private readonly IAccountService _accountService;
    private readonly IUser _user;

    public GetOrderByPaymentIntentQueryHandler(IAccountService accountService, IUser user)
    {
        _accountService = accountService;
        _user = user;
    }

    public Task<OrderDetailDto> Handle(GetOrderByPaymentIntentQuery request, CancellationToken cancellationToken)
        => _accountService.GetOrderByPaymentIntentAsync(_user.Id!, request.PaymentIntentId, cancellationToken);
}
