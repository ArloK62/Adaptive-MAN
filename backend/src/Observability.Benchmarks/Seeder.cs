using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Observability.Domain.Applications;
using Observability.Domain.Telemetry;
using Observability.Infrastructure.Persistence;
using DomainApplication = Observability.Domain.Applications.Application;

namespace Observability.Benchmarks;

internal static class Seeder
{
    public static async Task SeedAsync(
        string connectionString,
        DbContextOptions<ObservabilityDbContext> opts,
        string targetSessionId,
        int targetEvents,
        int fillerEvents,
        int crossProcessErrors)
    {
        var appId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var db = new ObservabilityDbContext(opts))
        {
            db.Applications.Add(new DomainApplication { Id = appId, Name = "Bench", Slug = "bench" });
            db.AppEnvironments.Add(new AppEnvironment { Id = envId, ApplicationId = appId, EnvironmentName = "dev" });
            db.Sessions.Add(new Session
            {
                ApplicationId = appId,
                EnvironmentId = envId,
                SessionId = targetSessionId,
                DistinctId = "bench-user",
                StartedAt = startedAt,
                LastSeenAt = startedAt,
            });
            await db.SaveChangesAsync();
        }

        var targetCorrelationIds = new List<string>(targetEvents);
        for (var i = 0; i < targetEvents; i++)
            targetCorrelationIds.Add($"corr-target-{i:D7}");

        await BulkInsertEventsAsync(connectionString, appId, envId, targetSessionId, targetCorrelationIds, startedAt, isTarget: true);

        if (fillerEvents > 0)
        {
            const int fillerSessions = 1_000;
            var perSession = Math.Max(1, fillerEvents / fillerSessions);
            for (var s = 0; s < fillerSessions; s++)
            {
                var sid = $"filler-session-{s:D5}";
                var corrIds = new List<string>(perSession);
                for (var i = 0; i < perSession; i++)
                    corrIds.Add($"corr-filler-{s:D5}-{i:D5}");
                await BulkInsertEventsAsync(connectionString, appId, envId, sid, corrIds, startedAt, isTarget: false);
            }
        }

        if (crossProcessErrors > 0)
        {
            await BulkInsertErrorsAsync(connectionString, appId, envId, targetCorrelationIds, crossProcessErrors, startedAt);
        }
    }

    private static async Task BulkInsertEventsAsync(
        string connectionString,
        Guid appId,
        Guid envId,
        string sessionId,
        List<string> correlationIds,
        DateTime startedAt,
        bool isTarget)
    {
        var table = new DataTable();
        table.Columns.Add("ApplicationId", typeof(Guid));
        table.Columns.Add("EnvironmentId", typeof(Guid));
        table.Columns.Add("EventName", typeof(string));
        table.Columns.Add("DistinctId", typeof(string));
        table.Columns.Add("SessionId", typeof(string));
        table.Columns.Add("CorrelationId", typeof(string));
        table.Columns.Add("NormalizedRoute", typeof(string));
        table.Columns.Add("EndpointGroup", typeof(string));
        table.Columns.Add("FeatureArea", typeof(string));
        table.Columns.Add("PropertiesJson", typeof(string));
        table.Columns.Add("ReleaseSha", typeof(string));
        table.Columns.Add("OccurredAt", typeof(DateTime));
        table.Columns.Add("CreatedAt", typeof(DateTime));

        for (var i = 0; i < correlationIds.Count; i++)
        {
            var occurredAt = startedAt.AddMilliseconds(i * 10);
            var row = table.NewRow();
            row["ApplicationId"] = appId;
            row["EnvironmentId"] = envId;
            row["EventName"] = isTarget && i % 5 == 0 ? "api_request_failed" : "page_viewed";
            row["DistinctId"] = isTarget ? "bench-user" : $"filler-{sessionId}";
            row["SessionId"] = sessionId;
            row["CorrelationId"] = correlationIds[i];
            row["NormalizedRoute"] = "/orders/{id}";
            row["EndpointGroup"] = "orders";
            row["FeatureArea"] = "billing";
            row["PropertiesJson"] = "{}";
            row["ReleaseSha"] = "abc1234";
            row["OccurredAt"] = occurredAt;
            row["CreatedAt"] = occurredAt;
            table.Rows.Add(row);
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        using var bulk = new SqlBulkCopy(sql)
        {
            DestinationTableName = "Events",
            BatchSize = 10_000,
            BulkCopyTimeout = 600,
        };
        foreach (DataColumn c in table.Columns)
            bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table);
    }

    private static async Task BulkInsertErrorsAsync(
        string connectionString,
        Guid appId,
        Guid envId,
        List<string> targetCorrelationIds,
        int errorCount,
        DateTime startedAt)
    {
        var table = new DataTable();
        table.Columns.Add("ApplicationId", typeof(Guid));
        table.Columns.Add("EnvironmentId", typeof(Guid));
        table.Columns.Add("Fingerprint", typeof(string));
        table.Columns.Add("FingerprintVersion", typeof(int));
        table.Columns.Add("ErrorType", typeof(string));
        table.Columns.Add("ExceptionType", typeof(string));
        table.Columns.Add("EndpointGroup", typeof(string));
        table.Columns.Add("JobName", typeof(string));
        table.Columns.Add("NormalizedRoute", typeof(string));
        table.Columns.Add("HttpStatusCode", typeof(int));
        table.Columns.Add("ReleaseSha", typeof(string));
        table.Columns.Add("PropertiesJson", typeof(string));
        table.Columns.Add("OccurrenceCount", typeof(long));
        table.Columns.Add("FirstSeenAt", typeof(DateTime));
        table.Columns.Add("LastSeenAt", typeof(DateTime));
        table.Columns.Add("LastCorrelationId", typeof(string));

        var clamped = Math.Min(errorCount, targetCorrelationIds.Count);
        for (var i = 0; i < clamped; i++)
        {
            var seenAt = startedAt.AddMilliseconds(i * 10 + 5);
            var row = table.NewRow();
            row["ApplicationId"] = appId;
            row["EnvironmentId"] = envId;
            row["Fingerprint"] = $"fp-{i:D7}";
            row["FingerprintVersion"] = 1;
            row["ErrorType"] = "server_error_occurred";
            row["ExceptionType"] = "InvalidOperationException";
            row["EndpointGroup"] = "orders";
            row["JobName"] = DBNull.Value;
            row["NormalizedRoute"] = "/orders/{id}";
            row["HttpStatusCode"] = 500;
            row["ReleaseSha"] = "abc1234";
            row["PropertiesJson"] = "{}";
            row["OccurrenceCount"] = 1L;
            row["FirstSeenAt"] = seenAt;
            row["LastSeenAt"] = seenAt;
            row["LastCorrelationId"] = targetCorrelationIds[i];
            table.Rows.Add(row);
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        using var bulk = new SqlBulkCopy(sql)
        {
            DestinationTableName = "Errors",
            BatchSize = 10_000,
            BulkCopyTimeout = 600,
        };
        foreach (DataColumn c in table.Columns)
            bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table);
    }
}
