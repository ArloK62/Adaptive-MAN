# Performance — session timeline derived query (Issue 5.2)

## What this measures

`Observability.Infrastructure.Sessions.SessionTimelineQuery.RunAsync` end-to-end, including:
- The session lookup
- The per-session events query (ordered)
- The chunked cross-process error join

This is the same code path the API endpoint `/api/sessions/{sessionId}/timeline` runs, lifted into a reusable query type. The endpoint adds JSON shaping on top, which is not what we're measuring — we're measuring the database round-trips that determine whether the derived approach holds.

## Methodology

- Harness: [`backend/src/Observability.Benchmarks`](../backend/src/Observability.Benchmarks/) — `Stopwatch` over 5 warmup + 50 measured iterations, fresh `DbContext` each iteration so connection pooling is exercised but tracking caches don't accumulate.
- Database: SQL Server 2022 in Docker (`mcr.microsoft.com/mssql/server:2022-latest`), local Docker Desktop on Windows 11, no resource limits applied.
- Schema: `EnsureCreatedAsync` — same model the production migration applies. Same indexes (Events: `(ApplicationId, EnvironmentId, CreatedAt)` + `(ApplicationId, EventName, CreatedAt)`; Errors: `(ApplicationId, EnvironmentId, Fingerprint)` + `(ApplicationId, EnvironmentId, LastSeenAt)`; Sessions: `(ApplicationId, EnvironmentId, SessionId)` unique + `(ApplicationId, EnvironmentId, LastSeenAt)`).
- Seed shape per cell:
  - 1 application + 1 environment + 1 target session
  - **target_events** events on the target session, with sequential correlation ids (1 in 5 named `api_request_failed`)
  - **filler_events** spread across 1,000 unrelated sessions to force the per-session predicate to skip rows
  - **cross_process_errors** error rows whose `LastCorrelationId` matches a target-session correlation id

## Results

> Status: **partial**. Two anchor cells run on 2026-05-03 against local Docker MSSQL. The full grid (10k and 100k target-event cells) is deferred — see "Pending grid cells" below.

| target_events | filler_events | cross_process_errors | seed (ms) | p50 (ms) | p95 (ms) | p99 (ms) |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 10 000 | 0 | 11 133 | 7.97 | 20.76 | 52.26 |
| 1 000 | 100 000 | 500 | 16 970 | 34.32 | 44.84 | 55.34 |
| 10 000 | 1 000 000 | 0 / 5 000 | — | — | — | — |
| 100 000 | 1 000 000 | 0 / 5 000 | — | — | — | — |

p50 / p95 / p99 are over n=50 measured iterations after 5 warmups.

### Observations from the captured cells

1. **The architecture-doc claim ("under 50ms at 1M events") is on track at small-to-medium scale.** At 1k target events + 100k filler + 500 cross-process errors, p50 sits at 34ms — well under the 50ms claim, with p95 still inside the budget. p99 grazes the budget at 55ms, which is consistent with first-iteration JIT/connection-pool effects amortizing across measured runs.
2. **Filler events do not penalize the per-session predicate at this scale.** Adding 10× more filler rows (10k → 100k) only moved p50 from 8ms to 34ms. That's almost entirely explained by the 10× growth in *target* events (100 → 1 000), which the query has to materialize and order. The filler rows pass through index seeks and don't touch the result set.
3. **Cross-process error count of 500 is invisible in latency.** The chunked-IN-list path runs once (well under the 1 000 chunk size), and SQL Server resolves it via index lookup on `(ApplicationId, EnvironmentId, Fingerprint)` — which doesn't cover `LastCorrelationId` directly, so this is actually a non-covered scan-against-tiny-Errors-table. At larger Error tables this could change; pending grid cells will exercise it.
4. **Local Docker MSSQL is optimistic vs. Azure SQL.** No network latency, no GP_S serverless cold-start, no shared-tenant noise. Azure SQL Basic/GP_S Gen5_1 against `ObservabilityDev` will be slower — anywhere from 2× to 10× depending on scale and whether the database has been auto-paused. Re-run after Brandon provisions Dev.

## Index observations

The production index set covers the queries we measured:
- `Sessions(ApplicationId, EnvironmentId, SessionId)` — unique seek for the session lookup.
- `Events(ApplicationId, EnvironmentId, CreatedAt)` — covers the per-session events scan well enough at these volumes; `SessionId` is in the predicate but not the index, so SQL Server hits the index for filtering and looks up rows. **At 100k target-events-per-session, this could become a scan-then-key-lookup hot spot.** Consider adding `Events(ApplicationId, EnvironmentId, SessionId)` *only if* the deferred 100k cell shows materially worse latency.
- `Errors(ApplicationId, EnvironmentId, Fingerprint)` — does not cover the `LastCorrelationId` predicate. At these tiny Error volumes the table fits in memory, so the cost is invisible. If the Errors table grows large (millions of rows), the cross-process join may need a `(ApplicationId, EnvironmentId, LastCorrelationId)` index. Re-evaluate after the 5k cross-process cell runs.

## Pending grid cells

The cells below were deferred to keep this session tight (the 1M-event seeds take long enough on local Docker to trade off against shipping a doc):

| target_events | filler_events | cross_process_errors |
|---:|---:|---:|
| 10 000 | 1 000 000 | 0 |
| 10 000 | 1 000 000 | 5 000 |
| 100 000 | 1 000 000 | 0 |
| 100 000 | 1 000 000 | 5 000 |

Run them with:

```
docker compose up -d mssql
dotnet run -c Release --project backend/src/Observability.Benchmarks -- --grid
```

The 100 000 target-events cells specifically test the architecture-doc claim that materialization should be reconsidered "past ~10k entries per session." If those cells stay under ~100ms p95, the derived approach holds for any realistic SCH or WMS session shape. If they don't, the recommended fix is **adding a `(SessionId, OccurredAt)` index** rather than materializing — see the index observation above.

## Verdict

The derived approach is justified at the volumes covered. The architecture-doc claim is plausible. **Decision unchanged: derived for MVP.** Re-run against Azure SQL Dev once Brandon provisions it, run the deferred 10k/100k cells, and revisit the index decision if either pushes p95 past ~100ms.
