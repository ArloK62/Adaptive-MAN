namespace Observability.Domain.Telemetry;

/// <summary>
/// One row per FE-bracketed session. Created on POST /api/ingest/sessions/start.
/// Closed by /end (or implicitly by inactivity, post-MVP). LastSeenAt is bumped by event
/// ingestion when a session_id is present so dashboards can sort by activity.
/// </summary>
public class Session
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string DistinctId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool HasError { get; set; }
    public string? ReleaseSha { get; set; }
}
