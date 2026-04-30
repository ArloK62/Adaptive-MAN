namespace Observability.Domain.Applications;

public class AppEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Application? Application { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public bool ReplayEnabled { get; set; }
    public string AllowedOriginsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
