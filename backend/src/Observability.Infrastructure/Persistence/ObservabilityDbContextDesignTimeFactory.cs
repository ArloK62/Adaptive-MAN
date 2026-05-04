using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Observability.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> resolve the DbContext without a live config or DI
/// container. EF doesn't connect during migration scaffolding — it only walks the model — so the
/// connection string here is a placeholder.
/// </summary>
public sealed class ObservabilityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ObservabilityDbContext>
{
    public ObservabilityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ObservabilityDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ObservabilityDesignTime;Trusted_Connection=True;")
            .Options;
        return new ObservabilityDbContext(options);
    }
}
