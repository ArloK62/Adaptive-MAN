using Microsoft.EntityFrameworkCore;
using Observability.Api.Endpoints;
using Observability.Infrastructure;
using Observability.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservabilityInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ObservabilityDbContext>();
    // Phase 0/1: EnsureCreated for fast local bring-up. Phase 2+: replace with MigrateAsync once an Initial
    // migration is generated (`dotnet ef migrations add Initial -p ../Observability.Infrastructure`).
    await db.Database.EnsureCreatedAsync();
}

app.MapHealthEndpoints();

app.Run();

public partial class Program { }
