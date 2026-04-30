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
| 4 – 9 | Open. Documented below. |

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

### Issue 4.1 — `observability-client-js` core API
**Description:** `init`, `identify`, `track`, `capturePageView`, `captureException`, `captureFailedRequest`. **Match the function signatures in `sch-ui/src/services/analytics.ts`** exactly.
**Acceptance criteria:**
- [ ] API matches `analytics.ts`
- [ ] Compile-time event allowlist via TS unions (preserve the existing safety net)
- [ ] Anonymous session ID in `sessionStorage`
- [ ] `identify(userId)` accepts only `string` — caller responsible for safety
- [ ] No-op silently if `init` not called
**Investigation questions:**
- Pull `analytics.ts` into the SDK as starting code, or rewrite from spec?

### Issue 4.2 — Route normalization utility (FE)
**Description:** Port `sch-ui/src/utils/routeUtils.ts` rules. Preserve the token threshold tuning that's already validated.
**Acceptance criteria:**
- [ ] Replaces numeric segments, UUIDs, ULIDs, tokens
- [ ] Drops query strings entirely
- [ ] Maps to feature areas
- [ ] Unit-tested against the SCH route fixture set + the `posthog-500-test` edge case

### Issue 4.3 — Axios + fetch interceptors
**Description:** Port the SCH_UI axios interceptor pattern from `apiClient.ts`.
**Acceptance criteria:**
- [ ] Axios module + native fetch wrapper
- [ ] Captures status_code, correlation_id, endpoint_group, method, is_network_error
- [ ] Documented usage; opt-in (no global monkey-patching)

### Issue 4.4 — React error boundary helper
**Description:** Port the SCH_UI `ErrorBoundary` pattern. **Never** sends message/stack/component-stack text.
**Acceptance criteria:**
- [ ] Captures `error_type`, `source`, `component_stack_depth` only
- [ ] Configurable fallback UI
- [ ] Never sends `error.message`, `error.stack`, React `componentStack` string

### Issue 4.5 — Batched send + retry
**Description:** Buffer in memory, flush on interval/size, exponential backoff with jitter, never throw.
**Acceptance criteria:**
- [ ] Configurable batch size + flush interval
- [ ] Exponential backoff with jitter on 5xx/network
- [ ] Drop after max retries; `console.warn` in dev only
- [ ] All SDK errors swallowed

### Issue 4.6 — `observability-client-dotnet`: implement `IAnalyticsService`
**Description:** **The .NET SDK's primary export must be a class implementing SCH's `IAnalyticsService` interface** (Capture, CaptureError, Shutdown). Copy the interface contract verbatim.
**Acceptance criteria:**
- [ ] `AdaptiveObservabilityService : IAnalyticsService`
- [ ] DI registration: `services.AddAdaptiveObservability(opts => ...)`
- [ ] `AnalyticsOptions`-shape config (Enabled, HostUrl, ApiKey, Environment, ReleaseSha)
- [ ] Async, non-blocking (background channel)
- [ ] Never throws into host app
**Investigation questions:**
- Ship the `IAnalyticsService` interface inside the SDK package, or assume the host app provides it?

### Issue 4.7 — Backend route normalization (.NET)
**Description:** Port the rules from `AnalyticsIdentity.cs`.
**Acceptance criteria:**
- [ ] Uses `RouteData` when available
- [ ] Falls back to regex normalization
- [ ] Unit-tested against SCH_API route fixtures

### Issue 4.8 — BG job failure dedup (server-side)
**Description:** **Resolves the deferred PostHog hardening item.** Suppress identical (job_name + error_type) failures within 15–30 min window. Server-side is canonical; SDK can also do best-effort client-side.
**Acceptance criteria:**
- [ ] `BackgroundJobFailures` table (Id, ApplicationId, EnvironmentId, JobName, ErrorType, Fingerprint, OccurrenceCount, FirstSeenAt, LastSeenAt, LastSuppressedAt, ReleaseSha)
- [ ] Window configurable per-app (default 15 min)
- [ ] `LastSuppressedAt` tracked
- [ ] Test: 100 identical failures within 5 min produce one incident with count=100

### Issue 4.9 — Reserve replay slot in `observability-client-js` (no rrweb yet)
**Description:** Define the public `init({ replay })` config shape and a no-op `replay` adapter interface so Phase 9 can drop in an rrweb implementation without changing call sites or breaking SemVer. **No rrweb dependency is added in this phase.**
**Acceptance criteria:**
- [ ] `InitOptions.replay` typed: `{ enabled, sampleRate, captureOnError, maskAllInputs, blockSelectors, maxSessionMinutes }`; defaults all off / safe
- [ ] `IReplayAdapter` interface with `start()`, `stop()`, `flush()`; default export is a no-op adapter that logs `replay disabled` in dev only
- [ ] `sessionId` is exposed to the adapter (same ID used by `track()` and Phase 5 sessions)
- [ ] No bundle-size impact (no rrweb import — adapter is a stub)
- [ ] Unit test: passing `replay.enabled: true` with no-op adapter is a no-op, not a throw
**Investigation questions:**
- Should the adapter slot also be present in the .NET SDK for symmetry, or is replay strictly FE? (Recommended: FE-only; do not pollute `IAnalyticsService`.)

### Issue 4.10 — SDK documentation + quickstarts
**Description:** READMEs and 5-minute quickstarts for both SDKs. Include the migration-from-PostHog cheatsheet for SCH.
**Acceptance criteria:**
- [ ] FE quickstart <50 LOC
- [ ] BE quickstart <50 LOC
- [ ] Both link to `docs/privacy-rules.md`
- [ ] PostHog→adaptive migration cheatsheet (import swap + DI swap)

---

## Phase 5 — Session Timeline

**Goal:** Per-session ordered timeline of events/errors/API failures in the dashboard. Replay-style debugging *without* recording screens.

**Exit criteria:** Clicking a session shows an ordered timeline including correlated backend errors.

### Issue 5.1 — `Sessions` + `SessionEvents` tables
**Description:** Sessions persisted on `session_started`. Materialized vs. derived timeline decided in 5.2.
**Acceptance criteria:**
- [ ] Tables match the Sessions model in this plan (Id, ApplicationId, EnvironmentId, SessionId, DistinctId, StartedAt, EndedAt, LastSeenAt, HasError, ReleaseSha)
- [ ] Indexes: (SessionId, OccurredAt)

### Issue 5.2 — Decide: derived vs. materialized timeline
**Description:** Spike both against synthetic 1M-event dataset.
**Acceptance criteria:**
- [ ] Spike PR with both implementations
- [ ] Latency + storage measurements
- [ ] Decision in `docs/architecture.md`

### Issue 5.3 — `POST /api/ingest/sessions/start` + `/end`
**Description:** FE SDK brackets a session.
**Acceptance criteria:**
- [ ] Start creates Sessions row
- [ ] End updates `EndedAt`
- [ ] Idempotent

### Issue 5.4 — `GET /api/sessions/{sessionId}/timeline`
**Description:** Ordered timeline of events + errors + API failures.
**Acceptance criteria:**
- [ ] Sorted by `OccurredAt`
- [ ] Each entry tagged (`event` | `error` | `api_failure`)
- [ ] Includes correlation IDs

### Issue 5.5 — Cross-process correlation
**Description:** Backend errors with the same `CorrelationId` as a FE `api_request_failed` event surface together.
**Acceptance criteria:**
- [ ] Backend errors joined to originating FE event by `CorrelationId`
- [ ] Timeline UI shows BE error inline under FE failure
**Investigation questions:**
- Confirm the correlation_id from SCH_API is a true request trace ID end-to-end (deferred PostHog risk).

### Issue 5.6 — Session timeline UI
**Description:** Vertical timeline rendering; type icons; "errors only" filter.
**Acceptance criteria:**
- [ ] Renders an ordered list (session_started → page_viewed → api_request_failed → frontend_exception → session_ended)
- [ ] Click → details drawer
- [ ] "Errors only" toggle

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
- **Whether to ship `IAnalyticsService` inside the .NET SDK** vs. assume host provides it — Phase 4.
- **Phase 9 replay:** Blob storage topology (per-env vs. per-app), direct-upload-via-SAS vs. proxy-through-API, default capture mode (recommended: `captureOnError`).

---

## Appendix A — PostHog Phase 1 inputs

This plan inherits from prior PostHog integration planning and implementation work on the SCH project. Key inputs:
- `POSTHOG_EVENT_CATALOG.md` — committed in both SCH repos; source of truth for the initial ported event catalog.
- `feature/posthog-implementation` branches in SCH_UI and SCH_API — production-bound implementations whose contracts (event names, identity rules, allowlists, route normalization) the new platform must preserve.
- Hardening prompts (SCH_API and SCH_UI) — the still-open items are folded into Phase 6 as cutover prerequisites.
- Phase 2 deferred event ideas — input to future event-catalog updates, not part of this plan's MVP.
