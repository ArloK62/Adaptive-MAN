# `adaptive-observability` — Development Plan

> Internal analytics, error-tracking, and session-timeline platform. Replaces an already-shipped PostHog Phase 1 integration in SCH with a custom system that onboards multiple internal apps under strict PHI/PII rules.

## Goal

Build a standalone repo that ingests safe events/errors from frontend + backend SDKs, persists them in Azure SQL, surfaces them in a React admin dashboard, and supports per-app onboarding with environment-specific keys and allowlists. Migrate SCH off PostHog onto this platform without losing any signal already captured.

## Scope (MVP)

Custom event ingestion · Custom error ingestion · Strict privacy allowlists · App/environment registration · API keys · React dashboard · Session timeline · React frontend SDK · .NET backend SDK · Azure Key Vault integration · **SCH PostHog→adaptive-observability migration**.

**Deferred (post-MVP, planned):** visual session replay via **rrweb** — designed for in Phase 4 (SDK leaves a slot), implemented in **Phase 9**. Disabled by default; gated on a separate privacy review.

**Deferred (out of scope, no plan):** autocapture, feature flags, A/B testing, funnels, heatmaps, surveys.

## Status

| Phase | State |
|---|---|
| 0 — Foundation & Repo Setup | **Done.** Removed from this doc; see `git log`. |
| 1 — Backend Ingestion MVP | **Done.** Removed from this doc; see `git log`. |
| 2 — Azure Key Vault & Deployment | **Partial.** KV config provider, fail-fast validation, and setup docs shipped (was 2.2 + 2.5). Dev vault `AdaptiveToolsKeyVault` (centralus, RBAC) holds four placeholder secrets tagged `purpose=adaptive-observability`. Hosting (2.3) and DB-secret cutover (2.4) are open below; UAT/Prod vault provisioning (2.1 partial) is open below. |
| 3 — React Dashboard MVP | **Done.** Removed from this doc; see `git log`. |
| 4 — Client SDKs | **Partial.** Both SDKs scaffolded with the full Phase 1 surface and 33 passing tests; shipped on `phase-4/client-sdks`. Outstanding work documented below (SCH fixture port, session-bracket auto-call, RouteData path, per-app dedup window). |
| 5 — Session Timeline | **Partial.** Sessions schema + ingest + derived timeline + cross-process correlation + UI shipped on `phase-5/session-timeline`; 31 backend tests including chunked-IN-list and orphan-`/end` regressions. Outstanding: 5.2 benchmark spike not run; SDK does not auto-bracket sessions (Issue 4.11); 5.5 end-to-end correlation-id verification owned by Phase 6.1. |
| 6 – 9 | Open. Documented below. |

## Constraints

- **Privacy first.** No patient names, emails, usernames, DOBs, raw URLs, query strings, request/response bodies, exception messages, stack traces, or JWTs. Allowlists enforced server-side at ingestion — unsafe fields are *rejected and logged*, not silently dropped. Rules already validated by the PostHog effort.
- **Azure-native.** ASP.NET Core 8/9, Azure SQL, Azure Key Vault with managed identity in deployed environments. **Azure Blob Storage** added in Phase 9 for replay chunks (not in MVP).
- **No new third-party dependencies without approval.** Phase 9 introduces `rrweb` + `rrweb-player` (MIT) — flagged as a net-new dependency requiring explicit approval at Phase 9 entry.
- **Separate repo** from SCH and other onboarded apps.
- **Contract continuity.** Event names, identity rules, allowed property shapes, and route normalization must match the existing `POSTHOG_EVENT_CATALOG.md` so SCH migration is a swap, not a rewrite.

## Existing assets to leverage (from PostHog Phase 1)

These are **inputs**, not duplicated work. The plan references them throughout.

**SCH_UI (`feature/posthog-implementation`):**
- `sch-ui/src/services/analytics.ts` — typed PostHog wrapper with compile-time event allowlist. **The new FE SDK API surface must match this** so cutover is import-line-only.
- `sch-ui/src/utils/routeUtils.ts` — route + endpoint normalization (strips IDs, UUIDs, tokens; maps to feature areas). Reuse the rules verbatim.
- `sch-ui/src/components/common/ErrorBoundary.tsx` — captures `error_type`, `source`, `component_stack_depth` only. Pattern is correct.
- `sch-ui/src/services/apiClient.ts` — Axios interceptor that emits `api_request_failed` with status_code, correlation_id, endpoint_group, method, is_network_error.
- `sch-ui/src/store/authStore.ts` — `posthog.identify(String(userId))` + `auth_login_success` / `auth_logout` flow.
- `sch-ui/src/main.tsx` — init pattern (autocapture: false, capture_pageview: false, replay disabled in prod, maskAllInputs).
- `sch-ui/src/App.tsx` — RouteTracker, global window error + unhandled rejection capture.

**SCH_API (`feature/posthog-implementation`):**
- `src/SCH.Core/Interfaces/IAnalyticsService.cs` — `Capture()`, `CaptureError()`, `Shutdown()`. **The new BE SDK must implement this interface** so migration is a DI registration swap.
- `src/SCH.Infrastructure/Services/Analytics/PostHogService.cs` — reference implementation; allowlist enrichment, swallows analytics failures.
- `src/SCH.Infrastructure/Services/Analytics/NullAnalyticsService.cs` — no-op pattern for disabled state.
- `src/SCH.Infrastructure/Services/Analytics/AnalyticsIdentity.cs` — distinct ID + route normalization rules.
- `src/SCH.Infrastructure/Services/Analytics/AnalyticsOptions.cs` — config shape (Enabled, HostUrl, ApiKey, Environment, ReleaseSha).
- `Program.cs` — conditional registration; dev-only test endpoints (must not exist in non-Dev).
- `GlobalExceptionMiddleware.cs` — `server_error_occurred` emission on true 500s.
- All 8 background services emit `background_job_failed` from catch blocks.

**Shared:**
- `POSTHOG_EVENT_CATALOG.md` — committed in both SCH repos. **Source of truth for the new platform's initial event catalog.**

**Identity rules (already live, must be preserved):**
- Human users: `String(userId)` (no `user_` prefix, no email/username/displayName)
- API clients: `api_client_{id}`
- Background jobs: `system:background-service`
- Dev test events: `test:dev`

**Phase 1 event set (already in code, must be preserved verbatim):**
`auth_login_success`, `auth_logout`, `page_viewed`, `api_request_failed`, `frontend_exception`, `server_error_occurred`, `background_job_failed`, plus dev-only `posthog_test_event` (renamed for the new platform).

**Deferred PostHog hardening items** (still open — folded into Phase 6 as cutover prerequisites):
- BG job failure dedup/cooldown (15–30 min window) on SCH_API
- `release_sha` populated in deployed environments
- Dev-only `/api/dev/posthog-test` locked to Development only
- Generic role names audit (no user-specific labels)
- Correlation ID confirmed as true request trace ID end-to-end
- `VITE_POSTHOG_KEY` / `VITE_POSTHOG_HOST` (and equivalents for the new platform) in `.env.example`
- UAT replay masking audit before any prod replay discussion
- `PostHog.AspNetCore v2.5.0` pre-release dependency monitored until removed in cutover

**Phase 2 deferred event ideas** (input to future event catalog updates, not part of this plan's MVP):
- SCH_UI: `order_created`, `order_submitted`, `report_generated`, `document_uploaded`
- SCH_API: `order_state_changed`, `claim_submission_failed`, `external_api_error`

## High-Level Architecture

```
Onboarded Apps (SCH_UI, SCH_API, SecondApp_UI, SecondApp_API, ...)
   │  ├── observability-client-js     (page events, FE exceptions, failed API calls, session ctx)
   │  └── observability-client-dotnet (server errors, job failures, correlation IDs, release meta)
   ▼
Observability API  (ingestion, validation, allowlist, dedupe, auth)
   ▼
Azure SQL          (Applications, Environments, Events, Errors, Sessions, ApiKeys, SafetyViolations, ...)
   ▼
React Admin Dashboard (health, error explorer, event explorer, session timeline, onboarding)
```

## Tech Stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core 8/9 + EF Core |
| Database | Azure SQL |
| Secrets | Azure Key Vault + managed identity |
| Hosting | Azure App Service or Container Apps |
| Frontend | React + TypeScript + Vite |
| Frontend libs | React Router, TanStack Query, Recharts, Tailwind, shadcn/ui |
| FE SDK | `packages/observability-client-js` (TS) — API shape mirrors SCH_UI's `analytics.ts`; lazy `replay/` submodule (Phase 9) |
| BE SDK | `packages/observability-client-dotnet` — implements SCH's `IAnalyticsService` (replay is FE-only; BE contract unchanged) |
| Replay (Phase 9) | `rrweb` (record) + `rrweb-player` (playback), MIT — gated on approval; off by default |
| Replay storage (Phase 9) | Azure Blob Storage (chunks) + Azure SQL metadata; **never** Azure SQL for chunk bodies |

## Repo Structure

```
adaptive-observability/
├── README.md
├── DEVELOPMENT_PLAN.md
├── docs/
│   ├── architecture.md
│   ├── privacy-rules.md
│   ├── event-catalog.md          (seeded from POSTHOG_EVENT_CATALOG.md)
│   ├── identity-rules.md
│   ├── route-normalization.md
│   ├── onboarding-guide.md
│   ├── azure-key-vault-setup.md
│   ├── api-contract.md
│   └── migration/posthog-to-adaptive.md
├── backend/
│   ├── src/
│   │   ├── Observability.Api/
│   │   ├── Observability.Application/
│   │   ├── Observability.Domain/
│   │   ├── Observability.Infrastructure/
│   │   └── Observability.Worker/
│   ├── tests/
│   │   ├── Observability.UnitTests/
│   │   └── Observability.IntegrationTests/
│   └── Dockerfile
├── frontend/
│   ├── src/{app,pages,components,services,hooks,types}/
│   └── Dockerfile
├── packages/
│   ├── observability-client-js/
│   └── observability-client-dotnet/
├── docker-compose.yml
└── .github/workflows/
```

---

# Phases

Each phase has a **Goal**, **Exit criteria**, and **Issues** ready to file in GitHub. Each issue follows:

```
### Title
**Description:** ...
**Acceptance criteria:**
- [ ] ...
**Investigation questions:**
- ...
```

---

## Phase 0 — Foundation & Repo Setup

**Status: Done.** Repo scaffolding, docs, docker-compose, CI, and `/health` endpoint are committed. See `git log` for details.

---

## Phase 1 — Backend Ingestion MVP

**Status: Done.** Domain entities, ingestion endpoints (`/api/ingest/events`, `/api/ingest/errors`), API key auth, allowlist validator, `SafetyViolations` write path, correlation-ID middleware, dev smoke test, and integration tests are committed. See `git log` for details.

---

## Phase 2 — Azure Key Vault & Deployment Setup

**Goal:** Deployed Observability API loads secrets from Key Vault via managed identity. No secrets in code or app settings.

**Exit criteria:** Backend in Azure (App Service or Container Apps) connects to Azure SQL using a connection string sourced from Key Vault, with no plaintext secrets in any committed file.

**Done in this phase already:**
- Issue 2.2 (KV config provider with fail-fast) — `backend/src/Observability.Api/Configuration/KeyVaultConfiguration.cs`.
- Issue 2.5 (`docs/azure-key-vault-setup.md`) — provisioning steps + rotation runbooks.
- Dev portion of 2.1 — `AdaptiveToolsKeyVault` (centralus, RBAC) holds four placeholder secrets tagged `purpose=adaptive-observability`. UAT/Prod vaults are not yet provisioned.

### Issue 2.1 — Provision UAT and Prod vaults

**Description:** Dev shares the existing `AdaptiveToolsKeyVault`. UAT and Prod still need dedicated `kv-observability-uat` and `kv-observability-prod` for blast-radius isolation. Provision alongside their respective hosting envs (no point standing up an empty vault).

**Acceptance criteria:**
- [ ] `kv-observability-uat`, `kv-observability-prod` provisioned in their App Service's region
- [ ] Soft-delete on both; purge protection on prod
- [ ] RBAC-only (no access policies); App Service MI granted `Key Vault Secrets User`, scoped to that vault only

**Decisions needed:**
- IaC tool — Bicep, Terraform, or stay on `az` CLI? An authoritative module should land before UAT.
- Cut Dev over to a dedicated `kv-observability-dev` at the same time, or leave it on the shared `AdaptiveToolsKeyVault`? (Shared is acceptable for Dev only; reconsider if other tools' principals start needing access.)

### Issue 2.3 — Hosting environment + managed identity for the Observability API

**Description:** The deployed API needs a hosting environment whose system-assigned managed identity has `Key Vault Secrets User` on its same-environment vault.

**Investigation findings (subscription `Adaptive Subscription`, snapshot 2026-04-30):**
- No App Services, App Service Plans, Function Apps, or Container Apps exist.
- `Microsoft.App` resource provider is **not registered**, so Container Apps requires an extra `az provider register -n Microsoft.App` step.
- One resource group exists (`AdaptiveTools`, centralus). Either colocate or spin up `rg-observability-{env}`.

**Recommended path:** App Service Linux + system-assigned MI in `centralus` (matches SQL + KV regions). Container Apps is viable but adds RP registration and ingress configuration.

**Acceptance criteria:**
- [ ] App Service Plan + App Service (or Container App) provisioned for Dev
- [ ] System-assigned MI enabled on the App Service
- [ ] MI granted `Key Vault Secrets User` on the Dev vault, scoped to that vault only
- [ ] `KeyVault__Uri` app setting points at the Dev vault
- [ ] `/health` returns 200 from the deployed API; KV-backed config resolves on startup

**Decisions needed:**
- **Hosting platform:** App Service Linux (recommended) vs Container Apps (requires RP registration first).
- **SKU for Dev:** B1 (~$13/mo), P0v3 (~$50/mo), or other.
- **Globally-unique app name:** e.g. `app-observability-dev`, `adaptive-observability-dev`.
- **Resource group:** new `rg-observability-dev` (recommended) vs colocate in `AdaptiveTools`.
- **Identity flavor:** system-assigned (simpler) vs user-assigned (portable across slots/apps).
- **Slot strategy:** single slot for Dev, or staging slot for blue/green?

### Issue 2.4 — Move database secret to Key Vault

**Description:** Replace the placeholder `ObservabilityDbConnection` in Key Vault with a real connection string and have the deployed API connect to Azure SQL through it. Read-only investigation surfaced complications the original plan didn't anticipate.

**Investigation findings (snapshot 2026-04-30):**
- One Azure SQL server: `adaptivetoolssql` (centralus). AAD admin: `brandon@adaptivesoftwarellc.com`.
- **SQL auth is disabled** (`azureAdOnlyAuthentication=true`). Username/password connection strings will not work — the deployed API must auth via Managed Identity (`Authentication=Active Directory Default` in the connection string).
- **Public network access is disabled** on the server. The App Service must reach SQL via VNet integration + a private endpoint, or the server's public-access posture must be reversed (regression of an explicit hardening decision).
- One paused database (`MaintenanceDB`, GP_S_Gen5_1 serverless). No `Observability*` database exists.
- The existing `DependencyInjection.cs` uses `UseSqlServer(connectionString)` which already handles `Authentication=Active Directory Default` — **no code change required** for MI auth.

**Path A (recommended):** Reuse `adaptivetoolssql` for Dev; create a new `ObservabilityDev` database; connect via MI through VNet-integrated App Service + private endpoint.

**Path B:** Stand up a dedicated `sql-observability-{env}` server. Cleaner isolation, more cost, more setup. Probably right for Prod; overkill for Dev.

**Acceptance criteria:**
- [ ] `ObservabilityDev` database created (Path A) or new SQL server + db (Path B)
- [ ] App Service can reach SQL (VNet integration + private endpoint, or alternative)
- [ ] App Service MI granted DB access via `CREATE USER [<app-name>] FROM EXTERNAL PROVIDER` + `db_datareader` / `db_datawriter` / `db_ddladmin`
- [ ] `ObservabilityDbConnection` secret in KV holds the real passwordless connection string
- [ ] `appsettings.*.json` in deployed envs contain no connection string
- [ ] Deployed API connects; ingestion smoke test writes a row

**Decisions needed:**
- **Server topology:** Path A (reuse `adaptivetoolssql`) vs Path B (dedicated server). Recommend A for Dev, decide separately for UAT/Prod.
- **Network access:** VNet-integrate App Service + add private endpoint (recommended) vs re-enable public network with a firewall rule (regression).
- **Database SKU:** `GP_S_Gen5_1` serverless (~$5–15/mo idle, matches `MaintenanceDB`) or fixed-capacity?
- **Migration strategy:** generate `dotnet ef migrations add Initial` and switch `EnsureCreatedAsync` → `MigrateAsync` *before* the first non-Dev deploy. Currently dev-only EnsureCreated is in place.
- **Human dependency:** the `CREATE USER … FROM EXTERNAL PROVIDER` T-SQL must be run by the AAD admin on SQL (`brandon@adaptivesoftwarellc.com`). No other principal can grant DB access. Capture his availability before the deploy window.

---

## Phase 3 — React Dashboard MVP

**Status: Done.** Dashboard shell, persistent app/env/date filter, health page with cards + sparklines, errors table + detail drawer, event explorer with JSON viewer + CSV export, sessions placeholder, and admin/apps page are committed. Backend `/api/apps` and `/api/dashboard/*` endpoints back the UI. Auth is a placeholder until Phase 8. See `git log` for details.

---

## Phase 4 — Client SDKs

**Goal:** SDKs whose API surfaces match SCH's existing `analytics.ts` and `IAnalyticsService` so SCH migration is mechanical, and so future apps onboard without custom tracking code.

**Exit criteria:** Both SDKs versioned and documented. A drop-in replacement PR in SCH (Phase 6) changes only imports, DI registration, and config — not call sites.

**Done in this phase already (on `phase-4/client-sdks`):**
- 4.1 — `observability-client-js` core API: `init`, `identify`, `track`, `capturePageView`, `captureException`, `captureFailedRequest`. Compile-time event allowlist via TS unions, sessionStorage session id, no-op-if-not-initialized. **Decision:** rewrote from spec rather than copying `analytics.ts` so the SDK has zero SCH-internal dependencies.
- 4.3 — Axios interceptor + native `fetch` wrapper. Opt-in.
- 4.4 — React error boundary that captures `error_type` / `source` / `component_stack_depth` only.
- 4.5 — Batched transport with size/interval flush, exponential backoff + jitter, all errors swallowed. Dev-only warnings gated by an `init({ debug })` flag.
- 4.6 — `AdaptiveObservabilityService : IAnalyticsService`, `AddAdaptiveObservability(...)` DI extension, background `Channel<T>`, never throws into host. **Decision:** the SDK ships its own `IAnalyticsService` interface (under `Adaptive.ObservabilityClient`); SCH adopters delete `SCH.Core.Interfaces.IAnalyticsService` and update `using` statements.
- 4.7 — Backend `RouteNormalizer.Normalize(path)` + `EndpointGroup(...)` + `NormalizeFromContext(HttpContext)`. **Caveat below: the `RouteData`/endpoint-template path was dropped in favor of `Request.Path` because endpoint-metadata reflection is fragile across MVC and Minimal APIs.**
- 4.8 — `BackgroundJobFailures` sidecar table with `LastSuppressedAt` + window-aware upsert; integration test confirms 100 identical failures collapse to one incident with `count=100`. **Caveat below: window is currently a static 15-minute default; per-app override is deferred to Phase 8.2 hardening.**
- 4.9 — Replay slot: `InitOptions.replay` shape, `IReplayAdapter` interface, default no-op adapter, no rrweb dependency. Unit test confirms `replay.enabled: true` with the no-op adapter is a no-op, not a throw.
- 4.10 — SDK READMEs (`packages/observability-client-js/README.md`, `packages/observability-client-dotnet/README.md`) with under-50-LOC quickstarts; migration cheatsheet at `docs/migration/posthog-to-adaptive.md`.

### Issue 4.2 — FE route normalization: validate against SCH fixture set

**Status:** the rules + token-threshold tuning are ported and unit-tested against a small fixture set including the `posthog-500-test` edge case. Outstanding: re-run the same normalizer against the *actual* SCH_UI fixture set (lives in the SCH repo) before SCH cutover, since that set is the validated regression suite.

**Acceptance criteria:**
- [ ] SCH_UI route fixture set imported (vendored or test-only fetched) into `packages/observability-client-js/src/__tests__/`
- [ ] All SCH fixtures pass against this normalizer with no diffs

**Decisions needed:**
- Vendor the fixture file into this repo, or run a one-off comparison from the SCH branch as a Phase 6 cutover prereq?

### Issue 4.7 — `RouteData`-aware path normalization

**Status:** path-based fallback is implemented (`RouteNormalizer.NormalizeFromContext` reads `Request.Path`). The `RouteData`/endpoint-template path stated in the original acceptance criteria was deliberately skipped because endpoint metadata APIs differ between MVC, Minimal APIs, and `IRouteDiagnosticsMetadata`, and getting it wrong silently breaks normalization without throwing.

**Acceptance criteria:**
- [ ] Decide whether the `RouteData` path is worth the surface-area cost (it primarily helps with MVC controllers that have catch-all parameters; Minimal APIs already produce `:id`-style normalization through plain path parsing)
- [ ] If yes: add it with explicit MVC + Minimal-API integration tests so a regression is caught
- [ ] Re-run against SCH_API route fixtures (same dependency as 4.2)

### Issue 4.8 — Per-app BG dedup window

**Status:** dedup table + window logic + 100-failure integration test are in place; window is a static 15-minute default in `IngestionService`. The original 4.8 acceptance criterion ("window configurable per-app") overlaps with Phase 8.2's "hardens (per-app override, audit of suppressed-vs-incident counts)." Proposed split: leave the static default in 4.8 as shipped, and explicitly own the per-app override in 8.2.

**Acceptance criteria:**
- [ ] Confirm split with the team; if 4.8 retains ownership, add a `BackgroundJobDedupWindow` column on `AppEnvironments` (nullable; falls back to static default) and thread it through the upsert
- [ ] If 8.2 takes ownership, mark this issue closed and update the 8.2 description

### Issue 4.11 — JS SDK auto-bracket sessions (Phase 5 integration gap)

**Description:** The JS SDK creates a session id in `init()` and stamps it on every event, but it does not call `POST /api/ingest/sessions/start` automatically. The Phase 5 backend's `BumpSessionAsync` will not fabricate a `Sessions` row from event traffic (by design — see [`docs/architecture.md`](docs/architecture.md)), so a freshly-onboarded app's `Sessions` table stays empty and `/api/sessions/{sessionId}/timeline` returns 404. End-to-end the platform has a hole until this lands.

**Acceptance criteria:**
- [ ] On the first `track()` / `capturePageView()` / `captureException()` after `init()`, the SDK fires-and-forgets `POST /api/ingest/sessions/start` with `{ session_id, distinct_id, release_sha }`. Subsequent events bump `LastSeenAt` server-side as already implemented.
- [ ] On `beforeunload` (browser) and `shutdown()` (programmatic), the SDK fires `POST /api/ingest/sessions/end`. Use `navigator.sendBeacon` for the unload path so it survives navigation.
- [ ] Idempotent: a second `start` call within the same session must not duplicate (server-side upsert already handles this, but the SDK shouldn't spam either).
- [ ] Optional `init({ trackSessions: false })` to disable for hosts that bracket sessions manually.
- [ ] Integration test that runs the JS SDK against the real ingestion API and confirms a `Sessions` row appears with `started_at` and `last_seen_at` populated.

**Investigation questions:**
- Should the .NET SDK also bracket sessions for server-rendered apps, or is session bracketing strictly a FE concern? (Leaning FE-only; server-side telemetry uses `system:*` distinct ids and rarely benefits from session timelines.)

---

## Phase 5 — Session Timeline

**Goal:** Per-session ordered timeline of events/errors/API failures in the dashboard. Replay-style debugging *without* recording screens.

**Exit criteria:** Clicking a session shows an ordered timeline including correlated backend errors.

**Done in this phase already (on `phase-5/session-timeline`):**
- 5.1 — `Sessions` table with `(ApplicationId, EnvironmentId, SessionId)` unique index and `(LastSeenAt)` index. **Decision (see 5.2 below):** no `SessionEvents` materialized table — timeline is derived at query time from `Events` + `Errors`.
- 5.3 — `POST /api/ingest/sessions/start` and `/end` under the api-key-protected ingest group; idempotent. A duplicate `/start` updates the existing row; an orphan `/end` (no prior `/start`) is dropped silently and the endpoint still returns 202 — previously inserted a malformed closing-only row.
- 5.4 — `GET /api/sessions/{sessionId}/timeline` returns ordered entries tagged `event` | `error`; `is_api_failure` boolean on event entries flags `api_request_failed`. Each entry carries its `correlation_id`. The cross-process error join chunks correlation ids in batches of 1,000 to stay under SQL Server's ~2,100-parameter IN-clause limit, with a regression test that exercises the chunked path.
- 5.5 — Cross-process correlation: backend errors that share a `CorrelationId` with any event in the session surface inline, tagged `source: "cross_process"`. The session row stamps `HasError = true` whenever any error ingestion arrives with a session id.
- 5.6 — Session timeline UI: vertical timeline with type-coded markers (event / api failure / FE error / BE cross-process error), errors-only toggle, sticky details drawer with raw JSON.

### Issue 5.2 — Spike PR for derived vs materialized

**Status:** the *decision* is recorded in [`docs/architecture.md`](docs/architecture.md) (derived for MVP, revisit when per-session entry counts push past ~10k). Benchmark harness lives at [`backend/src/Observability.Benchmarks`](backend/src/Observability.Benchmarks/); two anchor cells ran against local Docker MSSQL — results in [`docs/perf.md`](docs/perf.md). The 10k and 100k target-event cells are deferred (1M-event seeds take long enough on local Docker to trade off against shipping). Re-run against Azure SQL `ObservabilityDev` once Brandon provisions it.

**Acceptance criteria:**
- [x] Benchmark spike with seeded synthetic data; latency results recorded in `docs/perf.md` (partial — 2 of 8 grid cells; remainder deferred)
- [ ] Run the deferred 10k and 100k target-event cells (the materialization-breakeven test) — `dotnet run -- --grid` once Docker is convenient
- [ ] Re-run anchor cells against Azure SQL Dev after Phase 2.4 lands
- [ ] Confirm the derived approach holds at ingestion volumes from real onboarded apps

### Issue 5.5 — End-to-end correlation id propagation (cross-link to Phase 6)

**Status:** the join works correctly when both processes set the same `X-Correlation-Id`. SCH currently propagates correlation ids end-to-end *in PostHog code* but this has not been independently verified for the new platform's ingestion path. Already listed as a Phase 6.1 prereq; cross-linked here so the Phase 5 surface flags it.

**Acceptance criteria:**
- [ ] (Owned by Phase 6.1) Trace a single SCH UAT request from FE → BE → ingestion and confirm the same correlation id lands on both the FE `api_request_failed` event and the BE `server_error_occurred` error.

---

## Phase 6 — SCH Migration: PostHog → adaptive-observability

**Goal:** Cut SCH_UI and SCH_API over from PostHog to the new platform with zero signal loss and no privacy regressions.

**Exit criteria:** SCH_UI + SCH_API emit Phase 1 events to `adaptive-observability` UAT; parity validated against PostHog for 5 business days; PostHog decommissioned for SCH; zero `SafetyViolations`.

**Strategy:** Implement `IAnalyticsService` against the new platform → register both PostHog and adaptive-observability for a dual-write window in UAT → validate parity → swap registration to adaptive-only → remove `PostHog.AspNetCore` dependency.

### Issue 6.1 — Apply deferred PostHog hardening (prereqs)
**Description:** Resolve open hardening items before/alongside cutover so the migration starts from a known-good baseline.
**Acceptance criteria:**
- [ ] BG job dedup (15–30 min cooldown) live in SCH_API (or implemented in SDK; see 4.8)
- [ ] `release_sha` populated in deployed environments (UAT + Prod)
- [ ] `/api/dev/posthog-test` confirmed unreachable outside Development
- [ ] Generic role names audit complete on `auth_login_success` (no user-specific labels)
- [ ] Correlation ID is a true request trace ID end-to-end
- [ ] `.env.example` includes the new platform's keys (to replace `VITE_POSTHOG_KEY` / `VITE_POSTHOG_HOST`)
**Investigation questions:**
- Run hardening on PostHog branches first (low risk), or roll directly into the cutover PR?

### Issue 6.2 — Audit SCH_UI for migration touchpoints
**Description:** Catalog files that change. Expected based on the handoff: `analytics.ts`, `routeUtils.ts`, `main.tsx`, `App.tsx`, `apiClient.ts`, `authStore.ts`, `ErrorBoundary.tsx`, env files.
**Acceptance criteria:**
- [ ] Doc lists every file with a PostHog reference
- [ ] Doc lists every env var to swap

### Issue 6.3 — Implement `AdaptiveObservabilityClient` in `observability-client-js`
**Description:** Already done in Phase 4.1 — this issue is the SCH-side adoption.
**Acceptance criteria:**
- [ ] SCH_UI's `analytics.ts` rewires its underlying transport from PostHog SDK to the new SDK
- [ ] All `posthog.*` direct calls (in `main.tsx`, `authStore.ts`) replaced with the new SDK's equivalents
- [ ] Compile-time event allowlist preserved (no event names change)

### Issue 6.4 — Audit SCH_API for migration touchpoints
**Description:** Catalog files that change. Expected: `Program.cs` (DI), `appsettings.json` (config section rename), `PostHogService.cs` (replaced or kept dual), `SCH.Infrastructure.csproj` (drop `PostHog.AspNetCore`).
**Acceptance criteria:**
- [ ] Doc lists every file with a PostHog reference
- [ ] Doc lists every config key to swap

### Issue 6.5 — Implement `AdaptiveObservabilityService : IAnalyticsService` in `observability-client-dotnet`
**Description:** Already done in Phase 4.6 — this issue is the SCH-side adoption.
**Acceptance criteria:**
- [ ] SCH_API DI registration adds `AddAdaptiveObservability(...)` from new SDK
- [ ] No call site in `GlobalExceptionMiddleware.cs` or background services changes (because they consume `IAnalyticsService`, not the implementation)
- [ ] `appsettings.json` gains `AdaptiveObservability` section mirroring the existing `PostHog` shape

### Issue 6.6 — Dual-write window in UAT
**Description:** Register a composite `IAnalyticsService` that fans out to both PostHog and adaptive-observability for 5 business days in UAT. Compare counts daily.
**Acceptance criteria:**
- [ ] Composite implementation behind a feature flag
- [ ] Daily count comparison committed to a soak log
- [ ] Variance < 1% per event type for 5 consecutive days
**Investigation questions:**
- Variance threshold — 1%, 2%? Defines what "parity" means.

### Issue 6.7 — Cut PostHog off in UAT, then Prod
**Description:** After parity, flip composite to adaptive-only in UAT, soak 48h, then Prod.
**Acceptance criteria:**
- [ ] UAT on adaptive-only for 48h with zero `SafetyViolations`
- [ ] Prod cutover with rollback plan documented
- [ ] PostHog project for SCH archived (not deleted) for evidence retention

### Issue 6.8 — Remove `PostHog.AspNetCore` and PostHog FE SDK
**Description:** Final dependency removal once Prod is stable for 1 week.
**Acceptance criteria:**
- [ ] `PostHog.AspNetCore` removed from `SCH.Infrastructure.csproj`
- [ ] `posthog-js` (or whichever FE package) removed from `sch-ui/package.json`
- [ ] All `PostHog*` files deleted (PostHogService.cs etc.) — `IAnalyticsService` and `NullAnalyticsService` retained
- [ ] `POSTHOG_EVENT_CATALOG.md` renamed in both repos to `OBSERVABILITY_EVENT_CATALOG.md` (or replaced with link to platform docs)
- [ ] Rollback PR prepped just in case

### Issue 6.9 — SCH-specific dashboard preset
**Description:** Saved dashboard view in adaptive-observability with SCH selected by default. Replaces the planned "SCH Phase 1 Health Dashboard."
**Acceptance criteria:**
- [ ] Saved view reachable via dashboard nav
- [ ] Cards match the original PostHog dashboard plan

### Issue 6.10 — UAT soak + privacy validation
**Description:** Daily check of `SafetyViolations` for the soak window.
**Acceptance criteria:**
- [ ] Daily safety-violation log committed
- [ ] Privacy/compliance reviewer sign-off

---

## Phase 7 — Second App Onboarding

**Goal:** Onboard the second accessible app pair (`SecondApp_UI` + `SecondApp_API`). **No PostHog migration here** — this app is fresh-onboarding using the SDKs validated in Phase 6.

**Exit criteria:** Second app emits Phase 1 events with zero safety violations; multi-app dashboard switching validated.

### Issue 7.1 — Audit `SecondApp_UI`
**Description:** Routing, auth, API client, error boundaries, env config.
**Acceptance criteria:** `docs/audits/secondapp-ui.md` complete.

### Issue 7.2 — Audit `SecondApp_API`
**Description:** Middleware order, BG jobs, error handling, correlation IDs.
**Acceptance criteria:** `docs/audits/secondapp-api.md` complete.

### Issue 7.3 — `SECOND_APP_EVENT_CATALOG.md`
**Description:** App-specific events on top of the global Phase 1 set; global rules referenced, not duplicated.
**Acceptance criteria:**
- [ ] App-specific events listed with allowed props
- [ ] References `docs/event-catalog.md` for global rules

### Issue 7.4 — Onboard `SecondApp_UI`
**Description:** Mirror the post-migration SCH_UI integration pattern.
**Acceptance criteria:** Phase 1 events live, zero PHI, PR reviewed.

### Issue 7.5 — Onboard `SecondApp_API`
**Description:** DI-register `AddAdaptiveObservability(...)`; install error middleware; hook BG jobs.
**Acceptance criteria:** Phase 1 events live, zero exception messages/stacks.

### Issue 7.6 — Validate multi-app dashboard switching
**Description:** Filters scope cleanly across both apps; no cross-app data leakage.
**Acceptance criteria:**
- [ ] Manual smoke test against both apps
- [ ] Automated test foreshadowing Phase 8 RBAC: a user with access to App A cannot query App B's data

### Issue 7.7 — Third-app onboarding checklist
**Description:** Onboarding questions as a checklist file teams fill in before onboarding (frontend framework, backend framework, auth method, deployment env, DB type, correlation IDs supported, BG jobs, PHI/PII presence, never-replay pages).
**Acceptance criteria:**
- [ ] `docs/onboarding-checklist.md` committed

---

## Phase 8 — Alerts, Grouping & Production Hardening

**Goal:** Operate at production scale: alert on real incidents, group repeated errors, control access, retain data within policy.

**Exit criteria:** Production traffic from at least two onboarded apps; on-call gets only actionable alerts; RBAC enforced; retention job running on schedule.

### Issue 8.1 — Error fingerprinting (server-side hardening)
**Description:** Already present in 1.5; this hardens it (collision behavior, fingerprint version field).
**Acceptance criteria:**
- [ ] Fingerprint version stored on `Errors`
- [ ] Backfill job for past data
- [ ] Algorithm documented

### Issue 8.2 — BG job failure dedup hardening
**Description:** Already present in 4.8; this hardens (per-app override, audit of suppressed-vs-incident counts).
**Acceptance criteria:**
- [ ] Per-app window override
- [ ] Suppressed counts visible in dashboard

### Issue 8.3 — Alert rule engine
**Description:** Configurable rules.
**Acceptance criteria:**
- [ ] `AlertRules` table
- [ ] Types: count-over-window, new-error-after-release, error-rate-above-threshold, any-prod-job-failure
- [ ] Evaluator runs as `Worker` service

### Issue 8.4 — Notifications (email + Teams)
**Description:** Fire alerts to email + Microsoft Teams webhooks.
**Acceptance criteria:**
- [ ] Email via ACS or SendGrid (decide)
- [ ] Teams via incoming webhook
- [ ] Per-rule rate limit
**Investigation questions:**
- ACS vs. SendGrid — what does the company already use?

### Issue 8.5 — Retention policies
**Description:** Per-app retention with scheduled archive/delete. Replay retention is defined here but enforced once Phase 9 ships.
**Acceptance criteria:**
- [ ] Per-app setting (default 90d events, 180d errors, **14d replay** when Phase 9 lands)
- [ ] Worker runs nightly
- [ ] Audit log row per run
- [ ] Schema reserves a `ReplayRetentionDays` column on `AppEnvironments` (nullable until Phase 9)

### Issue 8.6 — RBAC
**Description:** Admin / Developer / Viewer / AppOwner.
**Acceptance criteria:**
- [ ] Roles persisted, applied at API + UI
- [ ] AppOwner cannot read other apps
- [ ] Admin/Developer access logged
**Investigation questions:**
- Identity source — Entra/AAD groups vs. local users?

### Issue 8.7 — Audit logging
**Description:** Audit dashboard access, settings changes, API key create/revoke.
**Acceptance criteria:**
- [ ] `AuditLogs` table
- [ ] All admin endpoints write audit rows
- [ ] Read-only audit view

### Issue 8.8 — Rate limiting + payload size limits
**Description:** Per-key rate; reject oversized payloads at the edge.
**Acceptance criteria:**
- [ ] Per-key req/sec configurable
- [ ] Default 64 KB payload max
- [ ] 429 + `Retry-After`

### Issue 8.9 — Ingestion queue
**Description:** Decouple receive from DB write at scale.
**Acceptance criteria:**
- [ ] Receive enqueues; worker drains
- [ ] In-process `Channel<T>` for MVP, Service Bus for scale
- [ ] Backpressure documented
**Investigation questions:**
- At what RPS does in-process backpressure stop being acceptable?

### Issue 8.10 — Index review + archival
**Description:** Review after first month of prod traffic.
**Acceptance criteria:**
- [ ] Slow-query review in `docs/perf.md`
- [ ] Indexes added with measured before/after

### Issue 8.11 — Key rotation runbook
**Description:** Exercise a Key Vault secret rotation in UAT.
**Acceptance criteria:**
- [ ] Runbook in `docs/azure-key-vault-setup.md`
- [ ] Rotation tested end-to-end

---

## Phase 9 — Session Replay (rrweb)

**Goal:** Add visual session replay via `rrweb`, scoped tightly: off by default, opt-in per app+env, masked aggressively, stored in Blob, retained briefly. Replay artifacts attach to the existing Phase 5 session timeline so debugging is "click the failure → watch the last 30 seconds."

**Exit criteria:** SCH UAT can opt in for a single feature area, capture-on-error mode produces a viewable replay attached to a `frontend_exception` event, masking audit signed off, prod remains disabled.

**Non-goals:** Always-on prod recording, full-session capture by default, replay of any surface flagged as PHI/PII.

**Dependency approval gate (blocks all Phase 9 issues):**
- [ ] `rrweb` + `rrweb-player` (MIT) approved as net-new dependencies
- [ ] Privacy/compliance sign-off on the masking policy in `docs/privacy-rules.md`
- [ ] Decision on Blob storage account topology (per-env vs. shared with lifecycle rules)

### Issue 9.1 — Decide replay scope and defaults
**Description:** Document what replay is and isn't for this platform. Lock defaults before any code lands.
**Acceptance criteria:**
- [ ] `docs/replay.md` covers: capture modes (always-on vs. capture-on-error vs. sampled), default = `captureOnError` with 30s circular buffer
- [ ] Per-app+env opt-in flag (`AppEnvironments.ReplayEnabled` already exists from Phase 1.1 — wired up here)
- [ ] Default `sampleRate` = 0; explicit per-app override required
- [ ] Prod cannot enable replay without an `ApprovedForProductionAt` timestamp set by an admin

### Issue 9.2 — Implement rrweb adapter in `observability-client-js`
**Description:** Replace the no-op adapter from Issue 4.9 with an rrweb-backed implementation. Lazy-loaded — only fetched if `replay.enabled: true`.
**Acceptance criteria:**
- [ ] `replay/` submodule code-split; main bundle size unchanged when replay is off
- [ ] `recorder.ts` calls `rrweb.record({ maskAllInputs, blockSelectors, ... })` from init config
- [ ] `buffer.ts` chunks events every 5–10s; in `captureOnError` mode keeps a 30s ring buffer and only flushes on error
- [ ] `transport.ts` gzip-compresses chunks (browser `CompressionStream`) and POSTs to `/api/ingest/replay/chunk` with `X-Session-Id` + `X-Chunk-Seq`
- [ ] Recorder stops at `maxSessionMinutes` (default 30) to bound storage per session
- [ ] `sessionId` matches the one used by `track()` — replay rows join cleanly to Phase 5 sessions

### Issue 9.3 — Centralized masking policy
**Description:** Masking config lives in one place per app, versioned, and shipped to the SDK at init. Selector lists for SCH must be reviewed by the same person who reviewed PostHog's privacy rules.
**Acceptance criteria:**
- [ ] `MaskingPolicies` table: Id, ApplicationId, EnvironmentId, Version, BlockSelectorsJson, MaskInputSelectorsJson, NeverRecordRoutesJson, CreatedAt, ApprovedByUserId
- [ ] FE SDK fetches the active policy at init (cached, with version pin)
- [ ] `MaskingPolicyVersion` stamped on every `SessionReplays` row so the policy in force at recording time is auditable
- [ ] SCH initial policy seeds: mask all inputs, block `[data-phi]`, `.patient-name`, `.dob`, etc. (port from any existing SCH replay-disable hints)

### Issue 9.4 — Domain models: `SessionReplays` + `SessionReplayChunks`
**Description:** Metadata in SQL, bytes in Blob.
**Acceptance criteria:**
- [ ] `SessionReplays`: Id, SessionId (FK), ApplicationId, EnvironmentId, StartedAt, EndedAt, ChunkCount, TotalBytes, MaskingPolicyVersion, ReleaseSha, CaptureMode (`always_on` | `capture_on_error` | `sampled`)
- [ ] `SessionReplayChunks`: Id, SessionReplayId, SeqNo, BlobUri, Bytes, ReceivedAt; unique index (SessionReplayId, SeqNo)
- [ ] `Sessions` row gains `HasReplay` derived flag (or computed view) so dashboards can filter
- [ ] EF migration is additive — Phase 1–8 schemas untouched

### Issue 9.5 — `POST /api/ingest/replay/chunk`
**Description:** Separate ingestion path. Public-key auth. Chunk written to Blob, metadata row inserted in SQL.
**Acceptance criteria:**
- [ ] Endpoint scoped to public_client keys; rejects server keys
- [ ] Hard cap 1 MB/chunk (per-app overridable); 413 on oversize
- [ ] Per-key replay-specific rate limit, separate from event ingestion (so replay storms can't starve analytics)
- [ ] Rejects if `AppEnvironments.ReplayEnabled = false` for the resolved app+env (defense-in-depth — the SDK should never have started, but server enforces too)
- [ ] Writes blob with content-addressed key: `{appSlug}/{env}/{sessionId}/{seqNo}.rrweb.gz`
- [ ] Inserts `SessionReplayChunks` row; on duplicate (SessionReplayId, SeqNo) returns 200 idempotently
**Investigation questions:**
- Single Blob container per env vs. per-app? (Per-env with prefix-based RBAC is simpler, per-app is cleaner for retention rules.)
- SAS-uploaded direct-to-Blob vs. proxy-through-API? Direct upload halves API CPU but complicates auth.

### Issue 9.6 — Replay viewer in the dashboard
**Description:** Add a player on the session timeline page (Phase 5.6). Streams chunks from Blob and feeds `rrweb-player`.
**Acceptance criteria:**
- [ ] Session timeline shows a "▶ Replay" affordance only when `HasReplay = true`
- [ ] Player loads chunks lazily in seq order, decompresses client-side
- [ ] Scrubber, speed control, and event markers from the timeline pinned to replay timestamps
- [ ] Viewer fetches chunks via short-lived signed URLs from the API (never expose blob credentials)
- [ ] Audit log row written when a replay is viewed (who, when, which session)

### Issue 9.7 — Capture-on-error wiring
**Description:** Make replay's killer feature trivial: any `captureException` or `captureFailedRequest` call optionally flushes the ring buffer.
**Acceptance criteria:**
- [ ] In `captureOnError` mode, error capture triggers `replay.flush()` before the event POST resolves
- [ ] Replay metadata links back to the triggering error's `CorrelationId`
- [ ] No replay flush if no error has occurred in the bounded session

### Issue 9.8 — Replay retention worker
**Description:** Specialize the Phase 8.5 retention job for replay. Replay TTL is much shorter than events.
**Acceptance criteria:**
- [ ] Default replay retention 14 days; per-app override
- [ ] Worker deletes Blob chunks first, then `SessionReplayChunks` rows, then `SessionReplays` row
- [ ] Failure to delete a blob is retried; never orphans bytes silently
- [ ] Audit log row per run with byte-count freed

### Issue 9.9 — RBAC for replay
**Description:** Replay is the most sensitive surface in the platform. Lock it down hardest.
**Acceptance criteria:**
- [ ] New role `ReplayViewer` (separate from `Developer`); not granted by default to anyone
- [ ] Even `Admin` does not get replay access without an explicit grant logged in `AuditLogs`
- [ ] AppOwner of one app cannot view another app's replays (already enforced by Phase 8 RBAC; covered by an integration test)
- [ ] Every replay view writes an audit row visible to compliance

### Issue 9.10 — UAT soak + masking audit
**Description:** Before any prod consideration, run replay in SCH UAT for 2 weeks, audit a stratified sample of recordings for any leaked PHI/PII.
**Acceptance criteria:**
- [ ] 2-week UAT soak with replay enabled on one feature area only
- [ ] Sample of N recordings reviewed by privacy reviewer; sign-off committed
- [ ] Zero leaked PHI/PII findings; any finding triggers masking policy bump and re-soak
- [ ] Storage cost / chunk-count metrics captured for capacity planning before any prod ramp
**Investigation questions:**
- Sample size N for masking audit? (Suggest: all recordings in week 1, stratified sample in week 2.)
- Do we need a "kill switch" config flag separate from `ReplayEnabled` to instantly disable replay across all apps in case of incident?

---

## Cross-Cutting

### Privacy review gates
- **Before Phase 6 SCH UAT cutover:** sign-off that ported event catalog matches `POSTHOG_EVENT_CATALOG.md` exactly.
- **Before Phase 6 SCH Prod cutover:** parity variance < threshold for 5 days; 48h UAT-on-adaptive-only with zero `SafetyViolations`.
- **Before Phase 9 (replay) entry:** rrweb dependency approved; masking policy reviewed; Blob storage topology decided; `docs/replay.md` committed.
- **Before Phase 9 prod enablement (per-app):** 2-week UAT masking audit clean; `ReplayViewer` RBAC in place; replay-specific retention job verified; admin-set `ApprovedForProductionAt` recorded.

### Migration risks (carried from PostHog hardening backlog)
- **Pre-release dependency:** SCH_API currently uses `PostHog.AspNetCore v2.5.0` pre-release. Plan removes it in Issue 6.8; until then, monitor for breaking changes.
- **Replay safety:** UAT replay masking has not been audited. Keep replay disabled until Phase 9 masking audit signs off; prod stays off-by-default per app even after sign-off.
- **Role names:** confirm `auth_login_success` `roles` property contains generic role names only, not user-specific labels.
- **Token threshold edge cases:** route normalization must not turn `posthog-500-test` into `posthog-{id}-test`. Port the validated SCH_UI threshold tuning verbatim.
- **4xx tracking:** explicitly out of scope for Phase 1. Decision deferred to a future event-catalog update.

### Verification (end-to-end test plan)
1. CI runs unit + integration tests on every PR.
2. **Phase 4 / 7 specific:** SDK quickstart emits each Phase 1 event; dashboard shows them under the correct app/env; submitting an unsafe event (`{ "email": "x@y.com" }`) returns 422 and writes a `SafetyViolations` row with no `Events` row.
3. **Phase 6 specific:** dual-write composite produces matching counts in PostHog and adaptive-observability for 5 business days.

### Still-open cross-cutting questions
- **IaC tool** (Bicep vs. Terraform vs. stay on `az` CLI) — needed before Phase 2 UAT/Prod provisioning.
- **Identity source for Phase 8 RBAC** (Entra/AAD groups vs. local users).
- **Email provider for alerts** (ACS vs. SendGrid) — Phase 8.
- **EF migration timing.** Phase 1 ships `EnsureCreatedAsync` for dev. Phase 4 + 5 added `BackgroundJobFailures` and `Sessions`, growing the schema. Phase 2.4 already owns the `migrations add Initial` + switch to `MigrateAsync` cutover; flagged here so the surface area is visible when that work runs.
- **Phase 9 replay:** Blob storage topology (per-env vs. per-app), direct-upload-via-SAS vs. proxy-through-API, default capture mode (recommended: `captureOnError`).

---

## Appendix A — PostHog Phase 1 inputs

This plan inherits from prior PostHog integration planning and implementation work on the SCH project. Key inputs:
- `POSTHOG_EVENT_CATALOG.md` — committed in both SCH repos; source of truth for the initial ported event catalog.
- `feature/posthog-implementation` branches in SCH_UI and SCH_API — production-bound implementations whose contracts (event names, identity rules, allowlists, route normalization) the new platform must preserve.
- Hardening prompts (SCH_API and SCH_UI) — the still-open items are folded into Phase 6 as cutover prerequisites.
- Phase 2 deferred event ideas — input to future event-catalog updates, not part of this plan's MVP.
