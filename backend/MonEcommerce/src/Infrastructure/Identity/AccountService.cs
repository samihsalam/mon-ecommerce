using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Infrastructure.Identity;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public AccountService(UserManager<ApplicationUser> userManager, IApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new AppNotFoundException(nameof(ApplicationUser), userId);

        var addresses = await LoadAddressesAsync(userId, cancellationToken);

        return new ProfileDto(user.Name, user.Email!, addresses);
    }

    public async Task<Result<ProfileDto>> UpdateProfileAsync(string userId, string name, string email, string? currentPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new AppNotFoundException(nameof(ApplicationUser), userId);

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        var trimmedName = name.Trim();

        if (emailChanged)
        {
            if (string.IsNullOrEmpty(currentPassword))
            {
                return Result<ProfileDto>.Failure(["Le mot de passe actuel est requis pour changer d'email."]);
            }

            if (!await _userManager.CheckPasswordAsync(user, currentPassword))
            {
                return Result<ProfileDto>.Failure(["Mot de passe actuel incorrect."]);
            }

            // Narrow TOCTOU: two different accounts racing to claim the same new email could
            // both pass this check before either writes. Same class of pre-existing gap Story
            // 2.1 already flagged (no unique index on NormalizedEmail, RequireUniqueEmail unset)
            // — not re-solved here, tracked at the root cause rather than patched per-caller.
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id)
            {
                return Result<ProfileDto>.Failure(["Un compte existe déjà avec cet email."]);
            }

            // Set Name on the tracked entity before the first Set*Async call: each one persists
            // immediately (via UserManager's internal UpdateUserAsync), and EF Core saves every
            // dirty property on the tracked entity in that same call — so Name rides along with
            // the first successful write instead of needing its own separate UpdateAsync, which
            // previously left a window where Email/UserName could be persisted while the overall
            // operation still reported failure (if that separate call then failed).
            user.Name = trimmedName;

            var setEmailResult = await _userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                return Result<ProfileDto>.Failure(setEmailResult.Errors.Select(e => e.Description));
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                return Result<ProfileDto>.Failure(setUserNameResult.Errors.Select(e => e.Description));
            }
        }
        else
        {
            user.Name = trimmedName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Result<ProfileDto>.Failure(result.Errors.Select(e => e.Description));
            }
        }

        var addresses = await LoadAddressesAsync(userId, cancellationToken);

        return Result<ProfileDto>.Success(new ProfileDto(user.Name, user.Email!, addresses));
    }

    private async Task<List<AddressDto>> LoadAddressesAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.Addresses
            .Where(a => a.UserId == userId)
            .Select(a => new AddressDto(a.Id, a.Street, a.City, a.PostalCode, a.Country))
            .ToListAsync(cancellationToken);
    }
}
