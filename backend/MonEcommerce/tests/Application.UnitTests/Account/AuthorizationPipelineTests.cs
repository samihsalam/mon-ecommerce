using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Account.Queries;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account;

// GetProfileQuery/UpdateProfileCommand are the first real usages of [Authorize] +
// AuthorizationBehaviour anywhere in this codebase (see Story 2.4 Dev Notes). AccountServiceTests
// exercises AccountService directly and never goes through the MediatR pipeline, so it can't
// prove the [Authorize] attribute is actually wired up and enforced end-to-end — this test
// builds the real pipeline (via AddApplicationServices, the exact registration Program.cs uses)
// to close that gap.
public class AuthorizationPipelineTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; }
        public List<string>? Roles { get; set; }
    }

    private class StubIdentityService : IIdentityService
    {
        public Task<string?> GetUserNameAsync(string userId) => Task.FromResult<string?>("stub-user");
        public Task<bool> IsInRoleAsync(string userId, string role) => Task.FromResult(true);
        public Task<bool> AuthorizeAsync(string userId, string policyName) => Task.FromResult(true);
        public Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password)
            => Task.FromResult((Result.Success(), userId: "stub-id"));
        public Task<Result> DeleteUserAsync(string userId) => Task.FromResult(Result.Success());
    }

    private class StubAccountService : IAccountService
    {
        public Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileDto("Alice", "alice@example.com", []));

        public Task<Result<ProfileDto>> UpdateProfileAsync(string userId, string name, string email, string? currentPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ProfileDto>.Success(new ProfileDto(name, email, [])));
    }

    private static IServiceProvider BuildServices(StubUser user)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddApplicationServices();
        builder.Services.AddSingleton<IUser>(user);
        builder.Services.AddSingleton<IIdentityService, StubIdentityService>();
        builder.Services.AddSingleton<IAccountService, StubAccountService>();

        return builder.Build().Services;
    }

    [Test]
    public void ShouldThrowUnauthorizedAccessExceptionWhenNoUserIsAuthenticated()
    {
        var services = BuildServices(new StubUser { Id = null });
        var mediator = services.GetRequiredService<IMediator>();

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await mediator.Send(new GetProfileQuery()));
    }

    [Test]
    public async Task ShouldReturnTheProfileWhenAnAuthenticatedUserSendsGetProfileQuery()
    {
        var services = BuildServices(new StubUser { Id = "user-1" });
        var mediator = services.GetRequiredService<IMediator>();

        var profile = await mediator.Send(new GetProfileQuery());

        Assert.That(profile.Name, Is.EqualTo("Alice"));
    }
}
