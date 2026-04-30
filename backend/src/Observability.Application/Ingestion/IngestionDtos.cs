using System.Text.Json;

namespace Observability.Application.Ingestion;

public sealed record EventIngestionRequest(
    string? Event,
    string? DistinctId,
    string? SessionId,
    DateTime? OccurredAt,
    Dictionary<string, JsonElement>? Properties);

public sealed record ErrorIngestionRequest(
    string? ErrorType,
    string? ExceptionType,
    string? DistinctId,
    string? SessionId,
    DateTime? OccurredAt,
    Dictionary<string, JsonElement>? Properties);

public sealed record IngestionContext(
    Guid ApplicationId,
    Guid EnvironmentId,
    string CorrelationId);

public enum IngestionOutcome
{
    Accepted = 0,
    SchemaError = 1,
    AllowlistViolation = 2
}

public sealed record IngestionResult(
    IngestionOutcome Outcome,
    string? RejectedField = null,
    string? Reason = null);
