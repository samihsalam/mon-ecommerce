using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Account.Queries;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, ProfileDto>
{
    private readonly IAccountService _accountService;
    private readonly IUser _user;

    public GetProfileQueryHandler(IAccountService accountService, IUser user)
    {
        _accountService = accountService;
        _user = user;
    }

    public Task<ProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
        => _accountService.GetProfileAsync(_user.Id!, cancellationToken);
}
