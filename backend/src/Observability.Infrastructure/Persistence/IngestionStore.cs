using Microsoft.EntityFrameworkCore;
using Observability.Application.Ingestion;
using Observability.Domain.Telemetry;

namespace Observability.Infrastructure.Persistence;

public sealed class IngestionStore : IIngestionStore
{
    private readonly ObservabilityDbContext _db;

    public IngestionStore(ObservabilityDbContext db) => _db = db;

    public async Task AddEventAsync(EventRecord record, CancellationToken ct)
    {
        _db.Events.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertErrorAsync(ErrorRecord record, CancellationToken ct)
    {
        var existing = await _db.Errors
            .FirstOrDefaultAsync(
                e => e.ApplicationId == record.ApplicationId
                  && e.EnvironmentId == record.EnvironmentId
                  && e.Fingerprint == record.Fingerprint,
                ct);

        if (existing is null)
        {
            _db.Errors.Add(record);
        }
        else
        {
            existing.OccurrenceCount++;
            existing.LastSeenAt = record.LastSeenAt;
            existing.LastCorrelationId = record.LastCorrelationId;
            if (!string.IsNullOrEmpty(record.ReleaseSha))
            {
                existing.ReleaseSha = record.ReleaseSha;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task AddSafetyViolationAsync(SafetyViolation violation, CancellationToken ct)
    {
        _db.SafetyViolations.Add(violation);
        await _db.SaveChangesAsync(ct);
    }
}
