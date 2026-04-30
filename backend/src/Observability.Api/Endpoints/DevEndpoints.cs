using Observability.Application.Ingestion;

namespace Observability.Api.Endpoints;

/// <summary>
/// Development-only smoke test endpoint. Mounted only when env=Development in Program.cs.
/// Equivalent to SCH_API's /api/dev/posthog-test, renamed for the new platform.
/// </summary>
public static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapPost("/api/dev/smoke-test", async (
            HttpContext http,
            IIngestionService service,
            CancellationToken ct) =>
        {
            // Self-emit using the dev_smoke_test event. Caller still needs a valid Dev API key on the header
            // because this routes through the same auth filter as /api/ingest/* in real usage; here it's
            // exempt so a fresh dev environment can confirm connectivity before keys exist.
            var ctx = new IngestionContext(
                Guid.Empty, Guid.Empty,
                (string?)http.Items["CorrelationId"] ?? Guid.NewGuid().ToString("N"));

            var result = await service.IngestEventAsync(
                new EventIngestionRequest(
                    Event: "dev_smoke_test",
                    DistinctId: "test:dev",
                    SessionId: null,
                    OccurredAt: DateTime.UtcNow,
                    Properties: null),
                ctx, ct);

            return Results.Ok(new { outcome = result.Outcome.ToString() });
        });
    }
}
