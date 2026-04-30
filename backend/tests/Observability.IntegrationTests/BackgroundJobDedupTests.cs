using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Observability.Infrastructure.Persistence;
using Xunit;

namespace Observability.IntegrationTests;

public class BackgroundJobDedupTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly IngestionWebApplicationFactory _factory;

    public BackgroundJobDedupTests(IngestionWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Hundred_identical_failures_collapse_to_one_incident_with_count_100()
    {
        await _factory.SeedAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Observability-Key", _factory.ServerKeyPlaintext);

        var payload = new
        {
            error_type = "TimeoutException",
            distinct_id = "system:background-service",
            occurred_at = DateTime.UtcNow,
            properties = new Dictionary<string, object?>
            {
                ["job_name"] = "nightly-import",
                ["error_type"] = "TimeoutException",
            },
        };

        for (var i = 0; i < 100; i++)
        {
            var res = await client.PostAsJsonAsync("/api/ingest/errors", payload);
            res.EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ObservabilityDbContext>();
        var rows = await db.BackgroundJobFailures
            .Where(b => b.ApplicationId == _factory.SeededAppId)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(100, rows[0].OccurrenceCount);
        Assert.Equal("nightly-import", rows[0].JobName);
        Assert.Equal("TimeoutException", rows[0].ErrorType);
        Assert.NotNull(rows[0].LastSuppressedAt);
    }
}
