namespace Observability.Domain.Applications;

public enum ApiKeyType
{
    PublicClient = 1,
    ServerApi = 2
}

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public ApiKeyType KeyType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CreatedByUserId { get; set; }

    public bool IsActive(DateTime now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}
