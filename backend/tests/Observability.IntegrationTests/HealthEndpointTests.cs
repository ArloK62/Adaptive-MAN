using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Observability.Infrastructure.Persistence;
using Xunit;

namespace Observability.IntegrationTests;

public sealed class HealthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"obs-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ObservabilityDb"] = "InMemory",
            });
        });
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<ObservabilityDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<ObservabilityDbContext>(opt => opt.UseInMemoryDatabase(_dbName));
        });
    }
}

public class HealthEndpointTests : IClassFixture<HealthWebApplicationFactory>
{
    private readonly HealthWebApplicationFactory _factory;

    public HealthEndpointTests(HealthWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
