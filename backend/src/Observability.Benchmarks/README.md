# Observability.Benchmarks

Phase 5.2 spike. Measures `SessionTimelineQuery.RunAsync` latency against synthetic data of varying shape.

## Prerequisites

- Docker Desktop running
- `docker compose up -d mssql` from repo root (uses the `mcr.microsoft.com/mssql/server:2022-latest` container in `docker-compose.yml`)
- Default connection string targets `localhost:1433` with the dev SA password from `docker-compose.yml`. Override via `OBS_BENCH_CONN`.

## Run a single cell

```
dotnet run -c Release --project backend/src/Observability.Benchmarks -- \
  --target-events 1000 --filler-events 100000 --cross-process-errors 500
```

Args:
- `--target-events N` — events on the measured session
- `--filler-events N` — events spread across 1,000 unrelated sessions (forces the per-session predicate to skip rows)
- `--cross-process-errors N` — errors sharing correlation ids with target-session events (exercises the chunked-IN-list path)
- `--skip-seed` — reuse last seed (useful when iterating on the query itself)

## Run the full grid

```
dotnet run -c Release --project backend/src/Observability.Benchmarks -- --grid
```

Prints a markdown results table to stdout. Cells reseed from scratch each time.

## What is measured

50 iterations of `SessionTimelineQuery.RunAsync` after 5 warmups. Each iteration uses a fresh `DbContext` so connection pooling is exercised but tracking caches don't accumulate. Reported: p50 / p95 / p99 in ms.

The seed is destructive — `EnsureDeletedAsync` then `EnsureCreatedAsync` runs first. (The bench project uses `EnsureCreatedAsync` so it doesn't depend on the EF Initial migration that ships in PR #6; switch to `MigrateAsync` once that lands so the bench schema applies via the same path as deployed envs.)
