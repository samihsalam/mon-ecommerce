using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Services;

// Uses a REAL UserManager (not a mock) — see Story 2.3's AuthServiceResetPasswordTests.cs for
// why: the thing under test includes Identity's own password/email validation, which a mock
// would trivially fake.
public class AccountServiceTests
{
    private ApplicationDbContext _context = null!;
    private ServiceProvider _provider = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private AccountService _accountService = null!;

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

        _accountService = new AccountService(_userManager, _context);
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _context.Dispose();
        _provider.Dispose();
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string password, string name = "Alice")
    {
        var user = new ApplicationUser { UserName = email, Email = email, Name = name };
        var result = await _userManager.CreateAsync(user, password);
        Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    [Test]
    public async Task GetProfileAsync_ShouldReturnNameEmailAndAddresses()
    {
        var user = await CreateUserAsync("alice@example.com", "password123");
        _context.Addresses.Add(new Address
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Street = "1 Rue de Paris",
            City = "Paris",
            PostalCode = "75001",
            Country = "France",
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var profile = await _accountService.GetProfileAsync(user.Id);

        Assert.That(profile.Name, Is.EqualTo("Alice"));
        Assert.That(profile.Email, Is.EqualTo("alice@example.com"));
        Assert.That(profile.Addresses, Has.Count.EqualTo(1));
        Assert.That(profile.Addresses[0].City, Is.EqualTo("Paris"));
    }

    [Test]
    public async Task UpdateProfileAsync_ShouldUpdateNameWithoutRequiringPasswordWhenEmailUnchanged()
    {
        var user = await CreateUserAsync("alice@example.com", "password123");

        var result = await _accountService.UpdateProfileAsync(user.Id, "Alice Updated", "alice@example.com", null);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.Name, Is.EqualTo("Alice Updated"));
        Assert.That(result.Value.Email, Is.EqualTo("alice@example.com"));
    }

    [Test]
    public async Task UpdateProfileAsync_ShouldFailWhenEmailChangedWithoutCurrentPassword()
    {
        var user = await CreateUserAsync("alice@example.com", "password123");

        var result = await _accountService.UpdateProfileAsync(user.Id, "Alice", "alice-new@example.com", null);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Le mot de passe actuel est requis pour changer d'email."));

        var reloaded = await _userManager.FindByIdAsync(user.Id);
        Assert.That(reloaded!.Email, Is.EqualTo("alice@example.com"));
    }

    [Test]
    public async Task UpdateProfileAsync_ShouldFailWhenEmailChangedWithWrongCurrentPassword()
    {
        var user = await CreateUserAsync("alice@example.com", "password123");

        var result = await _accountService.UpdateProfileAsync(user.Id, "Alice", "alice-new@example.com", "wrong-password");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Mot de passe actuel incorrect."));
    }

    [Test]
    public async Task UpdateProfileAsync_ShouldFailWhenNewEmailAlreadyBelongsToAnotherAccount()
    {
        await CreateUserAsync("taken@example.com", "password123", "Bob");
        var user = await CreateUserAsync("alice@example.com", "password123");

        var result = await _accountService.UpdateProfileAsync(user.Id, "Alice", "taken@example.com", "password123");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Un compte existe déjà avec cet email."));
    }

    [Test]
    public async Task UpdateProfileAsync_ShouldChangeEmailWhenCurrentPasswordIsCorrect()
    {
        var user = await CreateUserAsync("alice@example.com", "password123");

        var result = await _accountService.UpdateProfileAsync(user.Id, "Alice", "alice-new@example.com", "password123");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.Email, Is.EqualTo("alice-new@example.com"));

        var reloaded = await _userManager.FindByIdAsync(user.Id);
        Assert.That(reloaded!.Email, Is.EqualTo("alice-new@example.com"));
        Assert.That(reloaded.UserName, Is.EqualTo("alice-new@example.com"));
    }
}
