using Microsoft.EntityFrameworkCore;
using Observability.Domain.Telemetry;
using Observability.Infrastructure.Persistence;

namespace Observability.Infrastructure.Sessions;

public sealed record SessionTimelineResult(
    Session Session,
    IReadOnlyList<EventRecord> Events,
    IReadOnlyList<ErrorRecord> CrossProcessErrors,
    IReadOnlySet<string> CorrelationIds);

public static class SessionTimelineQuery
{
    public const int CrossProcessChunkSize = 1_000;

    public static async Task<SessionTimelineResult?> RunAsync(
        ObservabilityDbContext db,
        string sessionId,
        CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session is null)
            return null;

        var events = await db.Events.AsNoTracking()
            .Where(e => e.ApplicationId == session.ApplicationId
                     && e.EnvironmentId == session.EnvironmentId
                     && e.SessionId == sessionId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);

        var correlationIds = events
            .Select(e => e.CorrelationId)
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var directErrors = new List<ErrorRecord>();
        foreach (var chunk in correlationIds.Chunk(CrossProcessChunkSize))
        {
            var chunkList = chunk.ToList();
            var rows = await db.Errors.AsNoTracking()
                .Where(e => e.ApplicationId == session.ApplicationId
                         && e.EnvironmentId == session.EnvironmentId
                         && e.LastCorrelationId != null
                         && chunkList.Contains(e.LastCorrelationId))
                .ToListAsync(ct);
            directErrors.AddRange(rows);
        }

        var correlationIdSet = correlationIds.ToHashSet(StringComparer.Ordinal);
        return new SessionTimelineResult(session, events, directErrors, correlationIdSet);
    }
}
