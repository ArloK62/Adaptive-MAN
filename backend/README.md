# Observability backend

ASP.NET Core 8 solution. Five projects (`Api`, `Application`, `Domain`, `Infrastructure`, `Worker`) plus unit + integration tests.

## Build & run

```bash
dotnet restore Observability.sln
dotnet build Observability.sln
dotnet run --project src/Observability.Api/Observability.Api.csproj
```

`http://localhost:8080/health` returns `{ "status": "ok" }`.

## Local DB

Use the root `docker-compose up mssql` to bring up SQL Server. Connection string in `src/Observability.Api/appsettings.Development.json` points at `localhost,1433`.

In Development the API calls `EnsureCreatedAsync()` on startup. Replace with `MigrateAsync()` once an Initial migration exists:

```bash
dotnet ef migrations add Initial \
  -p src/Observability.Infrastructure \
  -s src/Observability.Api \
  -o Persistence/Migrations

dotnet ef database update \
  -p src/Observability.Infrastructure \
  -s src/Observability.Api
```

This must be done before the first UAT/Prod deploy.

## Tests

```bash
dotnet test tests/Observability.UnitTests
dotnet test tests/Observability.IntegrationTests
```

Integration tests use `WebApplicationFactory<Program>` with EF Core InMemory — no Docker required for the test pass.

## Layering

```
Api ──► Application ──► Domain
 │           ▲
 ▼           │
Infrastructure ─┘
```

`Api` and `Worker` reference `Application` + `Infrastructure`. `Domain` has no project dependencies.
