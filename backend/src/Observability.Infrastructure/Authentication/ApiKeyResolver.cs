using Microsoft.EntityFrameworkCore;
using Observability.Domain.Applications;
using Observability.Infrastructure.Persistence;

namespace Observability.Infrastructure.Authentication;

public sealed record ResolvedApiKey(Guid ApplicationId, Guid EnvironmentId, ApiKeyType KeyType);

public interface IApiKeyResolver
{
    Task<ResolvedApiKey?> ResolveAsync(string plaintextKey, CancellationToken ct);
}

public sealed class ApiKeyResolver : IApiKeyResolver
{
    private readonly ObservabilityDbContext _db;
    private readonly IApiKeyHasher _hasher;

    public ApiKeyResolver(ObservabilityDbContext db, IApiKeyHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<ResolvedApiKey?> ResolveAsync(string plaintextKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintextKey)) return null;

        var hash = _hasher.Hash(plaintextKey);
        var now = DateTime.UtcNow;

        var key = await _db.ApiKeys
            .AsNoTracking()
            .Where(k => k.KeyHash == hash)
            .Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now))
            .Select(k => new ResolvedApiKey(k.ApplicationId, k.EnvironmentId, k.KeyType))
            .FirstOrDefaultAsync(ct);

        return key;
    }
}
