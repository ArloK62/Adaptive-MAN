using Microsoft.AspNetCore.Mvc;
using Observability.Api.Middleware;
using Observability.Application.Ingestion;

namespace Observability.Api.Endpoints;

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/events", HandleEvent);
        group.MapPost("/errors", HandleError);
    }

    private static async Task<IResult> HandleEvent(
        [FromBody] EventIngestionRequest request,
        HttpContext http,
        IIngestionService service,
        CancellationToken ct)
    {
        var key = http.GetResolvedApiKey();
        var correlationId = (string)http.Items[CorrelationIdMiddleware.HttpItemKey]!;
        var ctx = new IngestionContext(key.ApplicationId, key.EnvironmentId, correlationId);

        var result = await service.IngestEventAsync(request, ctx, ct);
        return MapResult(result);
    }

    private static async Task<IResult> HandleError(
        [FromBody] ErrorIngestionRequest request,
        HttpContext http,
        IIngestionService service,
        CancellationToken ct)
    {
        var key = http.GetResolvedApiKey();
        var correlationId = (string)http.Items[CorrelationIdMiddleware.HttpItemKey]!;
        var ctx = new IngestionContext(key.ApplicationId, key.EnvironmentId, correlationId);

        var result = await service.IngestErrorAsync(request, ctx, ct);
        return MapResult(result);
    }

    private static IResult MapResult(IngestionResult result) => result.Outcome switch
    {
        IngestionOutcome.Accepted => Results.Accepted(),
        IngestionOutcome.SchemaError => Results.BadRequest(new { error = "schema_error", reason = result.Reason }),
        IngestionOutcome.AllowlistViolation => Results.UnprocessableEntity(new { error = "allowlist_violation", field = result.RejectedField, reason = result.Reason }),
        _ => Results.StatusCode(500)
    };
}
