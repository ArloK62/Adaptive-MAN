namespace Observability.Domain.Telemetry;

public class ErrorRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int FingerprintVersion { get; set; } = 1;
    public string ErrorType { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? EndpointGroup { get; set; }
    public string? JobName { get; set; }
    public string? NormalizedRoute { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ReleaseSha { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public long OccurrenceCount { get; set; } = 1;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string? LastCorrelationId { get; set; }
}
