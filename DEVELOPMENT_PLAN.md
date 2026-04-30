# `adaptive-observability` — Development Plan

> Internal analytics, error-tracking, and session-timeline platform. Replaces an already-shipped PostHog Phase 1 integration in SCH with a custom system that onboards multiple internal apps under strict PHI/PII rules.

## Goal

Build a standalone repo that ingests safe events/errors from frontend + backend SDKs, persists them in Azure SQL, surfaces them in a React admin dashboard, and supports per-app onboarding with environment-specific keys and allowlists. Migrate SCH off PostHog onto this platform without losing any signal already captured.

## Scope (MVP)

Custom event ingestion · Custom error ingestion · Strict privacy allowlists · App/environment registration · API keys · React dashboard · Session timeline · React frontend SDK · .NET backend SDK · Azure Key Vault integration · **SCH PostHog→adaptive-observability migration**.

**Deferred:** visual session replay, autocapture, feature flags, A/B testing, funnels, heatmaps, surveys.

## Constraints

- **Privacy first.** No patient names, emails, usernames, DOBs, raw URLs, query strings, request/response bodies, exception messages, stack traces, or JWTs. Allowlists enforced server-side at ingestion — unsafe fields are *rejected and logged*, not silently dropped. Rules already validated by the PostHog effort.
- **Azure-native.** ASP.NET Core 8/9, Azure SQL, Azure Key Vault with managed identity in deployed environments.
- **No new third-party dependencies without approval.**
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
| FE SDK | `packages/observability-client-js` (TS) — API shape mirrors SCH_UI's `analytics.ts` |
| BE SDK | `packages/observability-client-dotnet` — implements SCH's `IAnalyticsService` |

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

**Goal:** Repo that builds locally, has CI green, and seeds documentation from the validated PostHog Phase 1 work.

**Exit criteria:** `docker-compose up` brings up empty backend + frontend + DB; CI runs lint/build on PRs; `docs/` contains architecture, privacy, event catalog, identity, and route normalization docs derived from existing PostHog assets.

### Issue 0.1 — Create `adaptive-observability` repo and root scaffolding
**Description:** Initialize a new GitHub repo with the structure above. Add `README.md`, license, `.editorconfig`, `.gitignore`.
**Acceptance criteria:**
- [ ] Repo created, `main` default, branch protection on
- [ ] Root `README.md` summarizes platform purpose + links to `docs/`
- [ ] `.gitignore` ignores `bin/`, `obj/`, `node_modules/`, `dist/`, `*.user`, `appsettings.*.local.json`
- [ ] License decided
**Investigation questions:**
- Which GitHub org hosts this — same one that hosts SCH?
- Standard internal license/header file?

### Issue 0.2 — Write `docs/architecture.md`
**Description:** Document ingestion flow, dashboard flow, onboarding flow, deployment topology. Note migration path from PostHog.
**Acceptance criteria:**
- [ ] Sections: Overview, Ingestion path, Dashboard path, Onboarding path, Deployment topology, Migration-from-PostHog summary
- [ ] Diagrams (mermaid preferred)
- [ ] Reviewed by another engineer
**Investigation questions:**
- Mermaid in GitHub vs. checked-in PNGs — team norm?

### Issue 0.3 — Write `docs/privacy-rules.md` (seed from PostHog work)
**Description:** Codify forbidden vs. allowed fields. **Copy the validated lists from `POSTHOG_EVENT_CATALOG.md`** verbatim — these have already passed review.
**Acceptance criteria:**
- [ ] "Never store" list: patient names, emails, usernames, display names, DOBs, SSNs, policy/insurance IDs, clinical notes, raw URLs, query strings, request/response bodies, exception messages, stack traces, JWTs, React component stack text
- [ ] "Allowed fields" list: `app_id`, `environment`, `release_version`, `release_sha`, `normalized_route`, `endpoint_group`, `feature_area`, `http_status_code`, `correlation_id`, `exception_type`, `error_type`, `job_name`, `generic_role`, safe booleans/counters
- [ ] "Reject and log" rule documented (writes `SafetyViolations`)
- [ ] Replay disabled by default; UAT-only until separate privacy review
**Investigation questions:**
- Has compliance/legal already signed off on these lists for PostHog? If yes, reference the sign-off; if no, gate Phase 6 cutover on it.

### Issue 0.4 — Write `docs/event-catalog.md` (port `POSTHOG_EVENT_CATALOG.md`)
**Description:** Port the existing catalog. Preserve event names, allowed properties, and identity rules exactly.
**Acceptance criteria:**
- [ ] Events ported: `auth_login_success`, `auth_logout`, `page_viewed`, `api_request_failed`, `frontend_exception`, `server_error_occurred`, `background_job_failed`
- [ ] Each event has description, required props, allowed optional props, example JSON
- [ ] Catalog format chosen (code vs. markdown vs. DB) and decision recorded
- [ ] Phase 2 deferred events listed in an appendix (not in MVP)
**Investigation questions:**
- Source-of-truth: code (compile-time safety in SDKs) vs. DB (runtime hot-edit) vs. markdown (human-friendly)?

### Issue 0.5 — Write `docs/identity-rules.md`
**Description:** Lift the identity strategy from PostHog work.
**Acceptance criteria:**
- [ ] Human users: `String(userId)` (raw `sub` claim, e.g. `"42"`)
- [ ] API clients: `api_client_{id}`
- [ ] Background jobs: `system:background-service`
- [ ] Dev test: `test:dev`
- [ ] Explicit "do not use": `user_{id}`, email, username, displayName

### Issue 0.6 — Write `docs/route-normalization.md`
**Description:** Document the rules used by `routeUtils.ts` and `AnalyticsIdentity.cs`. Provides the spec the SDKs must implement.
**Acceptance criteria:**
- [ ] Rules: strip numeric IDs, UUIDs, ULIDs, tokens >= configured length
- [ ] FE example: `/patients/123/orders/456` → `/patients/:id/orders/:id`
- [ ] BE example: `/api/orders/123` → `orders` (endpoint group)
- [ ] Edge case noted: avoid token threshold producing `posthog-{id}-test` from `posthog-500-test`

### Issue 0.7 — Add `docker-compose.yml`
**Description:** mssql + backend + frontend up with one command.
**Acceptance criteria:**
- [ ] `docker-compose up` starts mssql, backend, frontend
- [ ] Backend runs migrations; frontend reaches backend
- [ ] README documents bring-up
**Investigation questions:**
- mssql-server vs. azure-sql-edge for closer parity?

### Issue 0.8 — CI pipeline
**Description:** GitHub Actions for backend, frontend, SDK packages.
**Acceptance criteria:**
- [ ] `backend.yml`, `frontend.yml`, `sdks.yml` green on initial empty PR
**Investigation questions:**
- npm vs. pnpm vs. yarn — match SCH_UI's choice for engineer mental-model continuity

### Issue 0.9 — Backend skeleton
**Description:** `.NET 8/9` solution with `Api`, `Application`, `Domain`, `Infrastructure`, `Worker` + test projects. Only `/health` endpoint.
**Acceptance criteria:**
- [ ] Solution builds clean
- [ ] `/health` returns 200 + version + git sha
- [ ] Layer references enforced
**Investigation questions:**
- .NET 8 LTS vs. 9 — what does SCH_API target?

### Issue 0.10 — Frontend skeleton
**Description:** Vite + React + TS + Tailwind + shadcn/ui shell.
**Acceptance criteria:**
- [ ] `pnpm dev` serves placeholder dashboard
- [ ] Build clean, ESLint + Prettier configured

### Issue 0.11 — EF Core migration setup
**Description:** Initial empty migration runnable locally.
**Acceptance criteria:**
- [ ] `dotnet ef database update` runs against docker-compose mssql
**Investigation questions:**
- EF migrations vs. DbUp/Flyway — what does SCH_API use?

---

## Phase 1 — Backend Ingestion MVP

**Goal:** Backend accepts safe events and errors from authenticated apps, validates against allowlists derived from the ported event catalog, and persists to Azure SQL.

**Exit criteria:** A test client can `POST /api/ingest/events` and `POST /api/ingest/errors` with a valid API key; safe fields persist; unsafe fields are rejected with a `SafetyViolations` row written; e2e integration test passes for every Phase 1 event from the PostHog catalog.

### Issue 1.1 — Domain models: `Application`, `AppEnvironment`
**Description:** EF entities for registered apps and per-environment config (Dev/UAT/Prod).
**Acceptance criteria:**
- [ ] `Applications`: Id, Name, Slug (unique), Description, CreatedAt, IsActive
- [ ] `AppEnvironments`: Id, ApplicationId, EnvironmentName, PublicClientKeyHash, ServerApiKeyHash, ReplayEnabled (default false), AllowedOrigins, CreatedAt, IsActive
- [ ] Unique index on (ApplicationId, EnvironmentName)
- [ ] Seed: `SCH` app with `Development`, `UAT`, `Production` environments
**Investigation questions:**
- AllowedOrigins JSON column vs. table?

### Issue 1.2 — `ApiKeys` model + hashing
**Description:** Hash-only storage. Two key types: `public_client` (FE), `server_api` (BE). Peppered SHA-256 with pepper from Key Vault.
**Acceptance criteria:**
- [ ] `ApiKeys`: Id, ApplicationId, EnvironmentId, KeyHash, KeyType, CreatedAt, ExpiresAt, RevokedAt, CreatedByUserId
- [ ] One-time plaintext display on create
- [ ] Lookup constant-time on indexed hash
**Investigation questions:**
- Key prefix scheme (`aopub_`, `aoserv_`) for dev usability?

### Issue 1.3 — Auth middleware
**Description:** Reads `X-Observability-Key`, hashes, looks up active key, attaches resolved app/env/key-type to `HttpContext`.
**Acceptance criteria:**
- [ ] Applied to `/api/ingest/*`
- [ ] 401 generic body on missing/invalid/revoked
- [ ] Public keys can hit only public-allowed endpoints
**Investigation questions:**
- Header name — `X-Observability-Key` vs. `Authorization: Bearer`?

### Issue 1.4 — `Events` table + `POST /api/ingest/events`
**Description:** Generic event ingestion. Validates against the event-catalog allowlist; stores allowed properties; returns 202.
**Acceptance criteria:**
- [ ] `Events`: Id, ApplicationId, EnvironmentId, EventName, DistinctId, SessionId, CorrelationId, NormalizedRoute, EndpointGroup, FeatureArea, PropertiesJson, ReleaseSha, CreatedAt
- [ ] Accepts the seven Phase 1 events from the ported catalog
- [ ] 202 success, 400 schema error, 422 allowlist violation
- [ ] Indexes: (ApplicationId, EnvironmentId, CreatedAt), (ApplicationId, EventName, CreatedAt)

### Issue 1.5 — `Errors` table + `POST /api/ingest/errors`
**Description:** Error ingestion with fingerprinting and occurrence aggregation.
**Acceptance criteria:**
- [ ] `Errors` per Errors model in this plan
- [ ] Repeat errors increment `OccurrenceCount`, update `LastSeenAt`
- [ ] No `ExceptionMessage` or `StackTrace` columns ever
- [ ] 202 on success
**Investigation questions:**
- Should `release_sha` be in the fingerprint, or surfaced separately so a bug "spans releases"?

### Issue 1.6 — Property allowlist validator
**Description:** Per-event-name allowlist sourced from the catalog. Drops unknown keys; writes `SafetyViolations` for known-forbidden keys (email, username, raw_url, exception_message, stack_trace, etc.).
**Acceptance criteria:**
- [ ] Allowlist sourced from a single artifact
- [ ] Unit-tested against every Phase 1 event
- [ ] Forbidden field names trigger violation row
**Investigation questions:**
- Source of truth: code vs. DB row?
- Capture field name only, or name + (redacted) length/type?

### Issue 1.7 — `SafetyViolations` table
**Description:** Records every rejected unsafe field. Never stores the offending value.
**Acceptance criteria:**
- [ ] Columns: Id, ApplicationId, EnvironmentId, EventName, RejectedField, Reason, CreatedAt
- [ ] No column ever stores the rejected value
- [ ] Index (ApplicationId, EnvironmentId, CreatedAt)

### Issue 1.8 — Correlation ID propagation
**Description:** Server reads `X-Correlation-Id`, stores on Events/Errors, generates one if missing.
**Acceptance criteria:**
- [ ] All Events/Errors persist `CorrelationId`
- [ ] Generated IDs are 128-bit ULIDs/UUIDs
**Investigation questions:**
- Match SCH_API's existing correlation-ID header — what is it today?

### Issue 1.9 — Integration tests for Phase 1 events
**Description:** End-to-end tests against containerized SQL Server, exercising every event from the ported catalog and every documented rejection case.
**Acceptance criteria:**
- [ ] Each Phase 1 event has a happy-path test
- [ ] Each forbidden field has a rejection test
- [ ] Auth: valid, missing, revoked, wrong-type-for-endpoint
- [ ] CI runs them

### Issue 1.10 — Dev-only smoke test endpoint
**Description:** Equivalent of SCH_API's `/api/dev/posthog-test`, scoped to Development only. Helps confirm ingestion end-to-end during local dev.
**Acceptance criteria:**
- [ ] Endpoint emits a `dev_smoke_test` event
- [ ] Compiled out of non-Development builds (or guarded by env check that fails closed)
- [ ] Documented in onboarding guide

---

## Phase 2 — Azure Key Vault & Deployment Setup

**Goal:** Deployed Observability API loads secrets from Key Vault via managed identity. No secrets in code or app settings.

**Exit criteria:** Backend in Azure App Service connects to Azure SQL using a connection string from `kv-observability-{env}`, with no plaintext secrets in any committed file.

### Issue 2.1 — Provision three Key Vaults (Dev/UAT/Prod)
**Description:** One vault per environment to limit blast radius.
**Acceptance criteria:**
- [ ] `kv-observability-dev`, `kv-observability-uat`, `kv-observability-prod` provisioned
- [ ] Soft-delete + purge protection on prod
- [ ] Access restricted to managed identities + small admin group
**Investigation questions:**
- Bicep vs. Terraform vs. portal+ARM — team standard?

### Issue 2.2 — Key Vault configuration provider
**Description:** Wire `Microsoft.Extensions.Configuration.AzureKeyVault`. Refuse startup if required secrets missing in non-Development.
**Acceptance criteria:**
- [ ] Required: `ObservabilityDbConnection`, `JwtSigningKey`, `ApiKeyHashPepper`, `EncryptionKey`
- [ ] Dev falls back to user secrets / `appsettings.Development.json`
- [ ] Fail-fast in UAT/Prod with clear log line

### Issue 2.3 — Managed identity for App Service
**Description:** Enable system-assigned MI; grant `get/list` on same-environment vault only.
**Acceptance criteria:**
- [ ] MI enabled on all three App Services
- [ ] Each MI scoped to its same-environment vault
- [ ] Verified via deployed test secret read
**Investigation questions:**
- System-assigned vs. user-assigned — team preference for portability?

### Issue 2.4 — Move database secret to Key Vault
**Description:** Store full SQL connection string (or AAD-auth config) in Key Vault.
**Acceptance criteria:**
- [ ] `ObservabilityDbConnection` set per env
- [ ] No `appsettings.*.json` contains UAT/Prod connection strings
- [ ] Backend connects in deployed envs
**Investigation questions:**
- AAD auth (MI → SQL) vs. SQL auth?

### Issue 2.5 — `docs/azure-key-vault-setup.md`
**Description:** Setup steps + secret rotation runbook.
**Acceptance criteria:**
- [ ] Step-by-step fresh-env setup
- [ ] Rotation runbook (DB password, hash pepper)
- [ ] Identity + secret flow diagram

---

## Phase 3 — React Dashboard MVP

**Goal:** Internal admin UI: filter by app + env + date range, see headline counts. **Replaces the planned "SCH Phase 1 Health Dashboard" that was going to live in PostHog.**

**Exit criteria:** Picking SCH + UAT + 24h shows accurate live counts. Cards mirror the original PostHog dashboard plan.

### Issue 3.1 — Dashboard shell
**Description:** Layout, sidebar nav, top bar with env switcher, route shell. Auth placeholder until Phase 8.
**Acceptance criteria:**
- [ ] Routes: `/health`, `/errors`, `/events`, `/sessions`, `/admin/apps`
- [ ] Placeholder login flagged with TODO

### Issue 3.2 — App + environment + date-range filters
**Description:** Persistent filter bar; selection in URL params + localStorage.
**Acceptance criteria:**
- [ ] Dropdowns from `/api/apps`
- [ ] Date range presets (1h, 24h, 7d, 30d, custom)
- [ ] All pages respect the filter

### Issue 3.3 — `GET /api/dashboard/health` + cards
**Description:** Mirrors the originally-planned PostHog dashboard cards.
**Acceptance criteria:**
- [ ] Cards: Backend 500s, FE exceptions, FE API failures, BG job failures, Page views (by feature_area), Login count, Top failing endpoint groups, Errors by release, Errors by environment, Recent sessions with errors
- [ ] Each card with sparkline (Recharts) + total
- [ ] Cards link to filtered Error/Event/Session views

### Issue 3.4 — Recent errors table
**Description:** Grouped by fingerprint, sortable by `LastSeenAt`/`OccurrenceCount`.
**Acceptance criteria:**
- [ ] Columns: ErrorType, Route/JobName, OccurrenceCount, LastSeenAt, Release, Env
- [ ] Click → error detail (occurrence timeline, sample correlation IDs, linked sessions)
- [ ] Server-side pagination

### Issue 3.5 — Event explorer
**Description:** Search/filter raw events.
**Acceptance criteria:**
- [ ] Filterable, server-paginated
- [ ] JSON properties viewer per row
- [ ] CSV export

### Issue 3.6 — Session list
**Description:** Recent sessions with error flag.
**Acceptance criteria:**
- [ ] Columns: SessionId, App/Env, DistinctId, StartedAt, EndedAt, HasError, ReleaseSha
- [ ] Click → session timeline (Phase 5)

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

### Issue 4.9 — SDK documentation + quickstarts
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
**Description:** Per-app retention with scheduled archive/delete.
**Acceptance criteria:**
- [ ] Per-app setting (default 90d events, 180d errors)
- [ ] Worker runs nightly
- [ ] Audit log row per run

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

## Cross-Cutting

### Privacy review gates
- **Before Phase 1 ships:** privacy doc reviewed; allowlist enforced server-side.
- **Before Phase 6 SCH UAT cutover:** sign-off that ported event catalog matches `POSTHOG_EVENT_CATALOG.md` exactly.
- **Before Phase 6 SCH Prod cutover:** parity variance < threshold for 5 days; 48h UAT-on-adaptive-only with zero `SafetyViolations`.
- **Before any visual replay (deferred beyond Phase 8):** separate privacy review; UAT-only; prod disabled by default; masking audit completed.

### Migration risks (carried from PostHog hardening backlog)
- **Pre-release dependency:** SCH_API currently uses `PostHog.AspNetCore v2.5.0` pre-release. Plan removes it in Issue 6.8; until then, monitor for breaking changes.
- **Replay safety:** UAT replay masking has not been audited. Keep replay disabled until Phase 8+ privacy review.
- **Role names:** confirm `auth_login_success` `roles` property contains generic role names only, not user-specific labels.
- **Token threshold edge cases:** route normalization must not turn `posthog-500-test` into `posthog-{id}-test`. Port the validated SCH_UI threshold tuning verbatim.
- **4xx tracking:** explicitly out of scope for Phase 1. Decision deferred to a future event-catalog update.

### Verification (end-to-end test plan)
1. `docker-compose up` — backend + frontend + mssql come up clean.
2. Use dashboard "Register App" (Phase 3+) to create a test app + Dev environment; copy public + server keys.
3. Run SDK quickstarts; emit each Phase 1 event.
4. Confirm dashboard: events under correct app/env, error fingerprints group, sessions render, no `SafetyViolations`.
5. Submit unsafe event (`{ "email": "x@y.com" }`); confirm 422, `SafetyViolations` row written, no `Events` row.
6. CI runs unit + integration tests on every PR.
7. **Phase 6 specific:** dual-write composite produces matching counts in PostHog and adaptive-observability for 5 business days.

### Open questions to resolve before Phase 0
- GitHub org for the new repo (same as SCH?)
- License / internal license header
- IaC tool (Bicep vs. Terraform)
- .NET version (8 LTS vs. 9; match SCH_API)
- Identity source for Phase 8 RBAC
- Mermaid vs. PNG for diagrams
- Package manager (npm/pnpm/yarn; match SCH_UI)
- Email provider for alerts (ACS vs. SendGrid)
- Event-catalog source-of-truth (code vs. DB vs. markdown)
- Whether to ship `IAnalyticsService` interface inside the .NET SDK or assume host provides it

---

## Appendix A — PostHog Phase 1 inputs

This plan inherits from prior PostHog integration planning and implementation work on the SCH project. Key inputs:
- `POSTHOG_EVENT_CATALOG.md` — committed in both SCH repos; source of truth for the initial ported event catalog.
- `feature/posthog-implementation` branches in SCH_UI and SCH_API — production-bound implementations whose contracts (event names, identity rules, allowlists, route normalization) the new platform must preserve.
- Hardening prompts (SCH_API and SCH_UI) — the still-open items are folded into Phase 6 as cutover prerequisites.
- Phase 2 deferred event ideas — input to future event-catalog updates, not part of this plan's MVP.
