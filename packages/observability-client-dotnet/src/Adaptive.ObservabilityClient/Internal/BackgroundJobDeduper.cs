using System.Collections.Concurrent;

namespace Adaptive.ObservabilityClient.Internal;

/// <summary>
/// Best-effort client-side dedup for repeated background-job failures. The server enforces
/// the canonical dedup window (Phase 4.8); this just spares the network round-trip.
/// Returns true if the failure should be sent; false if it was suppressed.
/// </summary>
internal sealed class BackgroundJobDeduper
{
    private readonly TimeSpan _window;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeen = new(StringComparer.Ordinal);

    public BackgroundJobDeduper(TimeSpan window, TimeProvider? clock = null)
    {
        _window = window;
        _clock = clock ?? TimeProvider.System;
    }

    public bool ShouldEmit(string jobName, string errorType)
    {
        var key = jobName + "|" + errorType;
        var now = _clock.GetUtcNow();
        var emit = true;
        _lastSeen.AddOrUpdate(
            key,
            _ => now,
            (_, prev) =>
            {
                if (now - prev < _window) emit = false;
                return emit ? now : prev;
            });
        return emit;
    }
}
