namespace Observability.Domain.Telemetry;

public class EventRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string DistinctId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? NormalizedRoute { get; set; }
    public string? EndpointGroup { get; set; }
    public string? FeatureArea { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public string? ReleaseSha { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
