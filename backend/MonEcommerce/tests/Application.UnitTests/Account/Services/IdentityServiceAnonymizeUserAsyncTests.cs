using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Services;

// Uses a REAL UserManager (not a mock) — same reasoning as AccountServiceTests.cs: the thing
// under test includes Identity's own email/username validation, which a mock would trivially fake.
public class IdentityServiceAnonymizeUserAsyncTests
{
    private ApplicationDbContext _context = null!;
    private ServiceProvider _provider = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private IdentityService _identityService = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddLogging();
        services.AddDataProtection();
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _userManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();

        _identityService = new IdentityService(
            _userManager,
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Mock.Of<IAuthorizationService>());
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _context.Dispose();
        _provider.Dispose();
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string name = "Alice")
    {
        var user = new ApplicationUser { UserName = email, Email = email, Name = name };
        var result = await _userManager.CreateAsync(user, "password123");
        Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    [Test]
    public async Task ShouldOverwriteNameEmailAndUserName()
    {
        var user = await CreateUserAsync("alice@example.com");

        var result = await _identityService.AnonymizeUserAsync(user.Id, "Utilisateur supprimé", "abc123@deleted.invalid");

        Assert.That(result.Succeeded, Is.True);

        var reloaded = await _userManager.FindByIdAsync(user.Id);
        Assert.That(reloaded!.Name, Is.EqualTo("Utilisateur supprimé"));
        Assert.That(reloaded.Email, Is.EqualTo("abc123@deleted.invalid"));
        Assert.That(reloaded.UserName, Is.EqualTo("abc123@deleted.invalid"));
    }

    [Test]
    public async Task ShouldMakeTheOldEmailUnableToResolveTheAccount()
    {
        var user = await CreateUserAsync("alice@example.com");

        await _identityService.AnonymizeUserAsync(user.Id, "Utilisateur supprimé", "abc123@deleted.invalid");

        var byOldEmail = await _userManager.FindByEmailAsync("alice@example.com");
        Assert.That(byOldEmail, Is.Null);
    }

    [Test]
    public async Task ShouldFailForAnUnknownUserId()
    {
        var result = await _identityService.AnonymizeUserAsync("does-not-exist", "Utilisateur supprimé", "abc@deleted.invalid");

        Assert.That(result.Succeeded, Is.False);
    }
}
