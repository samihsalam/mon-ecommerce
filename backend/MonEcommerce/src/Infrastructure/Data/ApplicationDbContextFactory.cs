using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MonEcommerce.Shared;

namespace MonEcommerce.Infrastructure.Data;

// EF's design-time tooling (dotnet ef migrations add/...) otherwise bootstraps the full Web host
// (Program.cs) to resolve ApplicationDbContext, which fails validation because unrelated services
// registered there (IEmailService, etc.) aren't configured in this environment. Implementing this
// factory lets EF build just the DbContext it needs directly, bypassing the app's DI container.
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Web"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString(Services.Database);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
