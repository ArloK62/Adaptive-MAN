using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Observability.Api.Endpoints;
using Observability.Api.Middleware;
using Observability.Infrastructure;
using Observability.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservabilityInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ObservabilityDbContext>();
    // Phase 0/1: EnsureCreated for fast local bring-up. Phase 2+: replace with MigrateAsync once an Initial
    // migration is generated (`dotnet ef migrations add Initial -p ../Observability.Infrastructure`).
    await db.Database.EnsureCreatedAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthEndpoints();

var ingest = app.MapGroup("/api/ingest").AddApiKeyAuth();
ingest.MapIngestionEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapDevEndpoints();
}

app.Run();

public partial class Program { }
