namespace Adaptive.ObservabilityClient;

/// <summary>
/// Migration seam for adopting the Adaptive Observability platform.
/// Mirrors the contract from SCH_API's <c>SCH.Core.Interfaces.IAnalyticsService</c> verbatim
/// so SCH (and similar callers) can swap implementations via DI without changing call sites.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Emits an analytics event. Never throws into the host app.</summary>
    void Capture(string eventName, string distinctId, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Emits an error event. Never throws into the host app.</summary>
    void CaptureError(string errorType, string distinctId, string? exceptionType = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Awaits in-flight sends and stops the background channel.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
