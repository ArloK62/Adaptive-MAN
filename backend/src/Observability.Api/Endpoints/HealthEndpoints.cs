using System.Reflection;

namespace Observability.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () =>
        {
            var asm = Assembly.GetExecutingAssembly();
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "unknown";
            var sha = Environment.GetEnvironmentVariable("RELEASE_SHA") ?? "local";
            return Results.Ok(new { status = "ok", version, sha });
        });
    }
}
