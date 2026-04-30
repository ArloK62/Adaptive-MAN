using System.Text.Json;

namespace Observability.Application.Ingestion;

public sealed record AllowlistResult(
    bool IsValid,
    string? RejectedField,
    string? Reason,
    Dictionary<string, JsonElement> SafeProperties);

public interface IPropertyAllowlistValidator
{
    AllowlistResult Validate(string eventName, IReadOnlyDictionary<string, JsonElement>? incoming);
}

/// <summary>
/// Per-event allowlist. Drops unknown keys silently; rejects forbidden keys with a violation.
/// </summary>
public sealed class PropertyAllowlistValidator : IPropertyAllowlistValidator
{
    public AllowlistResult Validate(string eventName, IReadOnlyDictionary<string, JsonElement>? incoming)
    {
        if (!EventCatalog.Phase1.TryGetValue(eventName, out var def))
        {
            return new AllowlistResult(false, eventName, "unknown_event", new Dictionary<string, JsonElement>());
        }

        var safe = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (incoming is null)
        {
            return CheckRequired(def, safe);
        }

        foreach (var (key, value) in incoming)
        {
            if (EventCatalog.ForbiddenProperties.Contains(key))
            {
                return new AllowlistResult(false, key, "forbidden_field", new Dictionary<string, JsonElement>());
            }

            if (def.AllowedProperties.Contains(key))
            {
                safe[key] = value;
            }
            // else: silently drop unknown keys (allowlist-style)
        }

        return CheckRequired(def, safe);
    }

    private static AllowlistResult CheckRequired(EventCatalog.EventDefinition def, Dictionary<string, JsonElement> safe)
    {
        foreach (var required in def.RequiredProperties)
        {
            if (!safe.ContainsKey(required))
            {
                return new AllowlistResult(false, required, "missing_required", new Dictionary<string, JsonElement>());
            }
        }
        return new AllowlistResult(true, null, null, safe);
    }
}
