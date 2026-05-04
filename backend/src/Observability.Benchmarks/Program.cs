using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Observability.Infrastructure.Persistence;
using Observability.Infrastructure.Sessions;

namespace Observability.Benchmarks;

internal static class Program
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=ObservabilityBench;User Id=sa;Password=Dev_Password_1!;TrustServerCertificate=true;Encrypt=false";

    private static async Task<int> Main(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("OBS_BENCH_CONN") ?? DefaultConnectionString;

        if (args.Contains("--grid"))
        {
            await RunGrid(conn);
            return 0;
        }

        var targetEvents = ParseArg(args, "--target-events", 1_000);
        var fillerEvents = ParseArg(args, "--filler-events", 100_000);
        var crossProcessErrors = ParseArg(args, "--cross-process-errors", 500);
        var skipSeed = args.Contains("--skip-seed");

        await RunOne(conn, targetEvents, fillerEvents, crossProcessErrors, skipSeed);
        return 0;
    }

    private static int ParseArg(string[] args, string key, int fallback)
    {
        var idx = Array.IndexOf(args, key);
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v)) return v;
        return fallback;
    }

    private static async Task RunGrid(string conn)
    {
        var grid = new (int target, int filler, int cross)[]
        {
            (100,     10_000,    0),
            (100,     10_000,    50),
            (1_000,   100_000,   0),
            (1_000,   100_000,   500),
            (10_000,  1_000_000, 0),
            (10_000,  1_000_000, 5_000),
            (100_000, 1_000_000, 0),
            (100_000, 1_000_000, 5_000),
        };

        Console.WriteLine($"| target | filler | cross | seed_ms | p50_ms | p95_ms | p99_ms |");
        Console.WriteLine($"|-------:|-------:|------:|--------:|-------:|-------:|-------:|");

        foreach (var cell in grid)
        {
            var r = await RunOne(conn, cell.target, cell.filler, cell.cross, skipSeed: false, quiet: true);
            Console.WriteLine($"| {cell.target,6} | {cell.filler,6} | {cell.cross,5} | {r.SeedMs,7} | {r.P50,6:F2} | {r.P95,6:F2} | {r.P99,6:F2} |");
        }
    }

    private record RunResult(long SeedMs, double P50, double P95, double P99);

    private static async Task<RunResult> RunOne(
        string conn,
        int targetEvents,
        int fillerEvents,
        int crossProcessErrors,
        bool skipSeed,
        bool quiet = false)
    {
        var opts = new DbContextOptionsBuilder<ObservabilityDbContext>()
            .UseSqlServer(conn)
            .Options;

        long seedMs = 0;
        const string targetSessionId = "bench-session";

        if (!skipSeed)
        {
            if (!quiet) Console.WriteLine($"[seed] target={targetEvents:N0} filler={fillerEvents:N0} cross={crossProcessErrors:N0}");
            var sw = Stopwatch.StartNew();
            await using (var db = new ObservabilityDbContext(opts))
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();
            }
            await Seeder.SeedAsync(conn, opts, targetSessionId, targetEvents, fillerEvents, crossProcessErrors);
            sw.Stop();
            seedMs = sw.ElapsedMilliseconds;
            if (!quiet) Console.WriteLine($"[seed] done in {seedMs} ms");
        }

        await using var ctxWarmup = new ObservabilityDbContext(opts);
        for (var i = 0; i < 5; i++)
            await SessionTimelineQuery.RunAsync(ctxWarmup, targetSessionId, CancellationToken.None);

        const int iterations = 50;
        var samples = new double[iterations];
        for (var i = 0; i < iterations; i++)
        {
            await using var ctx = new ObservabilityDbContext(opts);
            var sw = Stopwatch.StartNew();
            var result = await SessionTimelineQuery.RunAsync(ctx, targetSessionId, CancellationToken.None);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
            if (i == 0 && !quiet)
                Console.WriteLine($"[run] events={result?.Events.Count} cross_process_errors={result?.CrossProcessErrors.Count}");
        }
        Array.Sort(samples);
        var p50 = samples[iterations / 2];
        var p95 = samples[(int)(iterations * 0.95)];
        var p99 = samples[(int)(iterations * 0.99)];

        if (!quiet)
        {
            Console.WriteLine($"[result] p50={p50:F2}ms p95={p95:F2}ms p99={p99:F2}ms (n={iterations})");
        }
        return new RunResult(seedMs, p50, p95, p99);
    }
}
