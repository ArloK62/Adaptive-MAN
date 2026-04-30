using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Observability.Api.Middleware;
using Observability.Application.Ingestion;
using Observability.Domain.Telemetry;
using Observability.Infrastructure.Persistence;

namespace Observability.Api.Endpoints;

/// <summary>
/// Phase 5: session ingestion (start/end) + dashboard read endpoints (timeline).
/// Ingest endpoints sit under the api-key-protected /api/ingest group;
/// the read endpoint is unauthenticated alongside the rest of the dashboard until Phase 8.
/// </summary>
public static class SessionEndpoints
{
    public sealed record SessionStartRequest(string? SessionId, string? DistinctId, DateTime? StartedAt, string? ReleaseSha);
    public sealed record SessionEndRequest(string? SessionId, DateTime? EndedAt);

    public static void MapSessionIngestEndpoints(this RouteGroupBuilder ingest)
    {
        ingest.MapPost("/sessions/start", HandleStart);
        ingest.MapPost("/sessions/end", HandleEnd);
    }

    public static void MapSessionReadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{sessionId}/timeline", GetTimeline);
    }

    private static async Task<IResult> HandleStart(
        [FromBody] SessionStartRequest req,
        HttpContext http,
        IIngestionStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SessionId) || string.IsNullOrWhiteSpace(req.DistinctId))
            return Results.BadRequest(new { error = "schema_error", reason = "session_id_and_distinct_id_required" });

        var key = http.GetResolvedApiKey();
        var startedAt = req.StartedAt ?? DateTime.UtcNow;
        await store.UpsertSessionStartAsync(new Session
        {
            ApplicationId = key.ApplicationId,
            EnvironmentId = key.EnvironmentId,
            SessionId = req.SessionId,
            DistinctId = req.DistinctId,
            StartedAt = startedAt,
            LastSeenAt = startedAt,
            ReleaseSha = req.ReleaseSha,
        }, ct);
        return Results.Accepted();
    }

    private static async Task<IResult> HandleEnd(
        [FromBody] SessionEndRequest req,
        HttpContext http,
        IIngestionStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SessionId))
            return Results.BadRequest(new { error = "schema_error", reason = "session_id_required" });

        var key = http.GetResolvedApiKey();
        await store.UpsertSessionEndAsync(key.ApplicationId, key.EnvironmentId, req.SessionId, req.EndedAt ?? DateTime.UtcNow, ct);
        return Results.Accepted();
    }

    /// <summary>
    /// Derived timeline (Issue 5.2 decision): we union Events, Errors-with-this-session, and
    /// Errors-with-a-correlation_id-shared-by-this-session's-events at request time. No materialization
    /// for the MVP — see docs/architecture.md for the spike notes.
    ///
    /// Cross-process correlation (Issue 5.5): backend-only errors (server_error_occurred) won't have
    /// a session_id, but they will share a CorrelationId with the FE api_request_failed event that
    /// originated them. The second join surfaces those errors inline under the FE failure.
    /// </summary>
    private static async Task<IResult> GetTimeline(
        string sessionId,
        ObservabilityDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Results.BadRequest(new { error = "missing_session_id" });

        var session = await db.Sessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session is null)
            return Results.NotFound(new { error = "session_not_found", session_id = sessionId });

        var events = await db.Events.AsNoTracking()
            .Where(e => e.ApplicationId == session.ApplicationId
                     && e.EnvironmentId == session.EnvironmentId
                     && e.SessionId == sessionId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);

        var directErrors = await db.Errors.AsNoTracking()
            .Where(e => e.ApplicationId == session.ApplicationId
                     && e.EnvironmentId == session.EnvironmentId
                     && e.LastCorrelationId != null
                     && events.Select(ev => ev.CorrelationId).Contains(e.LastCorrelationId))
            .ToListAsync(ct);

        var correlationIds = events.Where(e => !string.IsNullOrEmpty(e.CorrelationId))
            .Select(e => e.CorrelationId!)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

        var entries = new List<object>();
        foreach (var ev in events)
        {
            entries.Add(new
            {
                kind = "event",
                occurred_at = ev.OccurredAt,
                id = ev.Id,
                event_name = ev.EventName,
                normalized_route = ev.NormalizedRoute,
                endpoint_group = ev.EndpointGroup,
                correlation_id = ev.CorrelationId,
                properties = TryParseJson(ev.PropertiesJson),
                is_api_failure = ev.EventName == "api_request_failed",
            });
        }
        foreach (var er in directErrors)
        {
            entries.Add(new
            {
                kind = "error",
                occurred_at = er.LastSeenAt,
                id = er.Id,
                error_type = er.ErrorType,
                exception_type = er.ExceptionType,
                endpoint_group = er.EndpointGroup,
                http_status_code = er.HttpStatusCode,
                correlation_id = er.LastCorrelationId,
                fingerprint = er.Fingerprint,
                occurrence_count = er.OccurrenceCount,
                source = correlationIds.Contains(er.LastCorrelationId ?? string.Empty) ? "cross_process" : "in_session",
            });
        }

        var ordered = entries.OrderBy(o => (DateTime)o.GetType().GetProperty("occurred_at")!.GetValue(o)!).ToList();

        return Results.Ok(new
        {
            session = new
            {
                session_id = session.SessionId,
                application_id = session.ApplicationId,
                environment_id = session.EnvironmentId,
                distinct_id = session.DistinctId,
                started_at = session.StartedAt,
                ended_at = session.EndedAt,
                last_seen_at = session.LastSeenAt,
                has_error = session.HasError,
                release_sha = session.ReleaseSha,
            },
            entries = ordered,
        });
    }

    private static JsonElement? TryParseJson(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        try { return JsonDocument.Parse(raw).RootElement.Clone(); }
        catch { return null; }
    }
}
