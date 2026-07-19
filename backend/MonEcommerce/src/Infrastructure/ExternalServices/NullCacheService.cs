using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Infrastructure.ExternalServices;

// Registered when Redis isn't configured (see DependencyInjection.cs) — always a cache miss,
// Set/Remove are no-ops. Without this fallback, any handler that constructor-injects
// ICacheService fails ASP.NET Core's ValidateOnBuild check in Development whenever Redis isn't
// configured, which it isn't in this dev environment. Same fix shape already recommended (but
// not yet applied) for IEmailService's equivalent gap — see deferred-work.md.
public class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
}
