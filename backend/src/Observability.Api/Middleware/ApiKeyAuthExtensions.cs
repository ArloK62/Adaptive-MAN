using Observability.Domain.Applications;
using Observability.Infrastructure.Authentication;

namespace Observability.Api.Middleware;

public static class ApiKeyAuthExtensions
{
    public const string HeaderName = "X-Observability-Key";
    public const string HttpItemKey = "ObservabilityApiKey";

    public static RouteGroupBuilder AddApiKeyAuth(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            if (!http.Request.Headers.TryGetValue(HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            var resolver = http.RequestServices.GetRequiredService<IApiKeyResolver>();
            var resolved = await resolver.ResolveAsync(header.ToString(), http.RequestAborted);
            if (resolved is null)
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            http.Items[HttpItemKey] = resolved;
            return await next(ctx);
        });

        return group;
    }

    public static ResolvedApiKey GetResolvedApiKey(this HttpContext context) =>
        (ResolvedApiKey)context.Items[HttpItemKey]!;
}
