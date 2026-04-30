using Observability.Domain.Telemetry;

namespace Observability.Application.Ingestion;

/// <summary>
/// Persistence boundary for ingestion. Implemented in Infrastructure (EF Core).
/// Kept as an interface so unit tests can substitute an in-memory store without EF.
/// </summary>
public interface IIngestionStore
{
    Task AddEventAsync(EventRecord record, CancellationToken ct);
    Task UpsertErrorAsync(ErrorRecord record, CancellationToken ct);
    Task AddSafetyViolationAsync(SafetyViolation violation, CancellationToken ct);
}
