namespace Observability.Domain.Telemetry;

public class SafetyViolation
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string RejectedField { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
