using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Observability.Application.Ingestion;

/// <summary>
/// Source-of-truth allowlist for Phase 1 events. Mirrors docs/event-catalog.md.
/// Hardcoded for compile-time safety; markdown is the human-readable view.
/// </summary>
public static class EventCatalog
{
    public sealed record EventDefinition(
        string Name,
        ImmutableHashSet<string> RequiredProperties,
        ImmutableHashSet<string> AllowedProperties);

    private static EventDefinition Def(string name, string[] required, string[] allowed) =>
        new(name,
            required.ToImmutableHashSet(StringComparer.Ordinal),
            required.Concat(allowed).ToImmutableHashSet(StringComparer.Ordinal));

    public static readonly FrozenDictionary<string, EventDefinition> Phase1 =
        new EventDefinition[]
        {
            Def("auth_login_success",
                required: Array.Empty<string>(),
                allowed: new[] { "generic_role", "release_sha" }),
            Def("auth_logout",
                required: Array.Empty<string>(),
                allowed: new[] { "release_sha" }),
            Def("page_viewed",
                required: new[] { "normalized_route" },
                allowed: new[] { "feature_area", "release_sha" }),
            Def("api_request_failed",
                required: new[] { "endpoint_group", "method", "http_status_code", "is_network_error" },
                allowed: new[] { "correlation_id", "release_sha" }),
            Def("frontend_exception",
                required: new[] { "error_type", "source" },
                allowed: new[] { "component_stack_depth", "normalized_route", "release_sha" }),
            Def("server_error_occurred",
                required: new[] { "exception_type", "endpoint_group" },
                allowed: new[] { "http_status_code", "correlation_id", "release_sha" }),
            Def("background_job_failed",
                required: new[] { "job_name", "error_type" },
                allowed: new[] { "release_sha" }),
            Def("dev_smoke_test",
                required: Array.Empty<string>(),
                allowed: new[] { "release_sha" }),
        }.ToFrozenDictionary(d => d.Name, StringComparer.Ordinal);

    /// <summary>
    /// Property keys that must always be rejected with a SafetyViolation, regardless of event.
    /// Source: docs/privacy-rules.md "Reject and log".
    /// </summary>
    public static readonly FrozenSet<string> ForbiddenProperties = new[]
    {
        "email", "username", "display_name", "displayName",
        "first_name", "last_name", "name", "full_name",
        "dob", "date_of_birth", "ssn",
        "raw_url", "url", "query_string", "querystring",
        "request_body", "response_body",
        "exception_message", "error_message", "message",
        "stack_trace", "stack", "component_stack",
        "jwt", "token", "access_token", "refresh_token", "bearer", "password",
        "policy_id", "insurance_id", "member_id",
        "user_id" // explicit: distinct_id is the only id field
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
