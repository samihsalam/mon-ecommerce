using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MonEcommerce.Infrastructure.Data;

// Story 5.4: SendGridEmailService needs to persist an EmailDispatchLog row from INSIDE an email
// handler that may itself be running mid-way through the ambient scoped ApplicationDbContext's
// own SaveChangesAsync (dispatched from DispatchDomainEventsInterceptor) — reusing that same
// instance there is an unsafe reentrant call EF Core rejects. An independent context instance
// sidesteps this.
//
// NOT built via EF Core's own AddDbContextFactory<T>: registering both AddDbContext<T> (Scoped,
// for the app's ambient IApplicationDbContext, with interceptors that need Scoped services like
// IUser) AND AddDbContextFactory<T> for the SAME T is invalid — both try to register
// DbContextOptions<T>, and the Singleton IDbContextFactory ends up depending on the Scoped
// DbContextOptions AddDbContext registers (caught by ValidateOnBuild in Development, but not by
// `dotnet build`/`test`, which never construct the real host). Even fixing the registration order
// doesn't fully solve it: AddDbContextFactory's options-configuration callback only ever receives
// the ROOT service provider (computed once, since DbContextOptions<T> becomes a Singleton), so it
// can't resolve Scoped interceptors like AuditableEntityInterceptor (needs IUser, itself Scoped)
// either way.
//
// This class sidesteps the whole problem: DbContextOptions<ApplicationDbContext> is built once,
// directly, with no DI resolution inside it at all (safe to share — DbContextOptions is immutable
// once built), and every CreateDbContext() call returns a genuinely new, independent
// ApplicationDbContext instance — deliberately with NO interceptors (EmailDispatchLog has no
// domain events of its own to dispatch, and there is no "acting user" for a system-sent email;
// SendGridEmailService sets EmailDispatchLog.Created itself instead of relying on
// AuditableEntityInterceptor).
//
// Not named ApplicationDbContextFactory — that name is already taken by the unrelated
// IDesignTimeDbContextFactory<ApplicationDbContext> `dotnet ef` CLI tooling uses (reads its own
// connection string straight from appsettings.json, deliberately bypassing the app's env-var
// configuration — a separate, pre-existing design-time-only concern from this runtime one).
public sealed class IndependentDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public IndependentDbContextFactory(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseSqlServer(connectionString);
        builder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        _options = builder.Options;
    }

    public ApplicationDbContext CreateDbContext() => new(_options);
}
