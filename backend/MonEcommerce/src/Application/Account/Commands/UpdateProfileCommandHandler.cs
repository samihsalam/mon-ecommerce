using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Account.Commands;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccountService _accountService;
    private readonly IUser _user;

    public UpdateProfileCommandHandler(IAccountService accountService, IUser user)
    {
        _accountService = accountService;
        _user = user;
    }

    public Task<Result<ProfileDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        => _accountService.UpdateProfileAsync(_user.Id!, request.Name, request.Email, request.CurrentPassword, cancellationToken);
}
