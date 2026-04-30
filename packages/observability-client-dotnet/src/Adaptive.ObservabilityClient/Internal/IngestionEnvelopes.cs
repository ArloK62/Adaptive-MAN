using System.Text.Json.Serialization;

namespace Adaptive.ObservabilityClient.Internal;

internal enum EnvelopeKind { Event, Error }

internal sealed record QueuedEnvelope(EnvelopeKind Kind, object Payload, int Attempts);

// Backend uses snake_case (Program.cs sets JsonNamingPolicy.SnakeCaseLower); explicit names lock it in.
internal sealed class EventEnvelope
{
    [JsonPropertyName("event")] public string Event { get; init; } = string.Empty;
    [JsonPropertyName("distinct_id")] public string DistinctId { get; init; } = string.Empty;
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("occurred_at")] public DateTime OccurredAt { get; init; }
    [JsonPropertyName("properties")] public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}

internal sealed class ErrorEnvelope
{
    [JsonPropertyName("error_type")] public string ErrorType { get; init; } = string.Empty;
    [JsonPropertyName("exception_type")] public string? ExceptionType { get; init; }
    [JsonPropertyName("distinct_id")] public string DistinctId { get; init; } = string.Empty;
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("occurred_at")] public DateTime OccurredAt { get; init; }
    [JsonPropertyName("properties")] public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}
