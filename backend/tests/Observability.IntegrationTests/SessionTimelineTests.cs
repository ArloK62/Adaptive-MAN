using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Observability.Api.Middleware;
using Xunit;

namespace Observability.IntegrationTests;

public class SessionTimelineTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly IngestionWebApplicationFactory _factory;
    public SessionTimelineTests(IngestionWebApplicationFactory factory) => _factory = factory;

    private HttpClient AuthClient(string key)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add(ApiKeyAuthExtensions.HeaderName, key);
        return c;
    }

    [Fact]
    public async Task Session_lifecycle_and_timeline_with_cross_process_correlation()
    {
        await _factory.SeedAsync();
        var publicClient = AuthClient(_factory.PublicKeyPlaintext);
        var serverClient = AuthClient(_factory.ServerKeyPlaintext);
        var dashboard = _factory.CreateClient();

        const string sid = "session-abc";
        const string distinct = "user-42";
        const string corr = "corr-xyz-1";

        // 1. /sessions/start
        var startRes = await publicClient.PostAsJsonAsync("/api/ingest/sessions/start", new
        {
            session_id = sid,
            distinct_id = distinct,
            release_sha = "abc1234",
        });
        Assert.Equal(HttpStatusCode.Accepted, startRes.StatusCode);

        // 2. FE event with a session_id
        var pageView = await publicClient.PostAsJsonAsync("/api/ingest/events", new
        {
            @event = "page_viewed",
            distinct_id = distinct,
            session_id = sid,
            occurred_at = DateTime.UtcNow.AddMinutes(-2),
            properties = new Dictionary<string, object?> { ["normalized_route"] = "/orders/:id" },
        });
        Assert.Equal(HttpStatusCode.Accepted, pageView.StatusCode);

        // 3. FE api_request_failed event with a correlation_id (this is what the BE error will pivot on)
        // The server overwrites event.correlation_id from the X-Correlation-Id header, so set that.
        publicClient.DefaultRequestHeaders.Remove("X-Correlation-Id");
        publicClient.DefaultRequestHeaders.Add("X-Correlation-Id", corr);
        var apiFail = await publicClient.PostAsJsonAsync("/api/ingest/events", new
        {
            @event = "api_request_failed",
            distinct_id = distinct,
            session_id = sid,
            occurred_at = DateTime.UtcNow.AddMinutes(-1),
            properties = new Dictionary<string, object?>
            {
                ["endpoint_group"] = "orders",
                ["method"] = "POST",
                ["http_status_code"] = 500,
                ["is_network_error"] = false,
            },
        });
        Assert.Equal(HttpStatusCode.Accepted, apiFail.StatusCode);

        // 4. Server emits the matching server_error_occurred WITHOUT a session_id but WITH the correlation_id.
        serverClient.DefaultRequestHeaders.Remove("X-Correlation-Id");
        serverClient.DefaultRequestHeaders.Add("X-Correlation-Id", corr);
        var serverErr = await serverClient.PostAsJsonAsync("/api/ingest/errors", new
        {
            error_type = "ServerError",
            exception_type = "System.InvalidOperationException",
            distinct_id = "system:api",
            occurred_at = DateTime.UtcNow,
            properties = new Dictionary<string, object?>
            {
                ["endpoint_group"] = "orders",
                ["http_status_code"] = 500,
            },
        });
        Assert.Equal(HttpStatusCode.Accepted, serverErr.StatusCode);

        // 5. /sessions/end
        var endRes = await publicClient.PostAsJsonAsync("/api/ingest/sessions/end", new { session_id = sid });
        Assert.Equal(HttpStatusCode.Accepted, endRes.StatusCode);

        // 6. GET timeline
        var timelineRes = await dashboard.GetAsync($"/api/sessions/{sid}/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineRes.StatusCode);
        var json = await timelineRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var session = doc.RootElement.GetProperty("session");
        Assert.Equal(sid, session.GetProperty("session_id").GetString());
        Assert.Equal(distinct, session.GetProperty("distinct_id").GetString());
        Assert.True(session.GetProperty("ended_at").ValueKind != JsonValueKind.Null);

        var entries = doc.RootElement.GetProperty("entries");
        var kinds = entries.EnumerateArray().Select(e => e.GetProperty("kind").GetString()!).ToList();
        Assert.Contains("event", kinds);
        Assert.Contains("error", kinds); // cross-process error joined in by correlation_id

        // The server error should appear with source=cross_process (no session_id of its own)
        var crossProcess = entries.EnumerateArray().FirstOrDefault(e =>
            e.GetProperty("kind").GetString() == "error"
            && e.GetProperty("correlation_id").GetString() == corr);
        Assert.True(crossProcess.ValueKind != JsonValueKind.Undefined);
        Assert.Equal("cross_process", crossProcess.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Timeline_returns_404_for_unknown_session()
    {
        await _factory.SeedAsync();
        var dashboard = _factory.CreateClient();
        var res = await dashboard.GetAsync("/api/sessions/does-not-exist/timeline");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
