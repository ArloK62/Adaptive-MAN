using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Observability.Infrastructure.Persistence;

namespace Observability.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddObservabilityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ObservabilityDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ObservabilityDb is not configured.");

        services.AddDbContext<ObservabilityDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        return services;
    }
}
