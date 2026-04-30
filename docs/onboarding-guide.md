# Onboarding Guide

How to add a new app to `adaptive-observability`. Detailed Phase 4+ — this file is a placeholder for now.

## Prerequisites

- App is in scope per [privacy-rules.md](privacy-rules.md).
- Owner has access to the Observability admin dashboard.

## Steps

1. **Register the app.** Dashboard → Admin → Apps → New. Provide slug + name.
2. **Create environments.** `Development`, `UAT`, `Production`. Configure `AllowedOrigins` per env.
3. **Generate keys.** One `public_client` (FE), one `server_api` (BE), per environment. Copy plaintext immediately — it cannot be recovered.
4. **Install SDKs** (Phase 4 — not yet shipped). FE: `observability-client-js`. BE: `observability-client-dotnet`.
5. **Configure.** Set `host`, `key`, `environment`, `releaseSha` per env.
6. **Smoke test.** Emit a `dev_smoke_test` event from a Development build; confirm it appears in the dashboard.

## Privacy review checklist (per app, before UAT)

- [ ] Identity rule confirmed: no email/username/displayName in `distinct_id`.
- [ ] Routes go through normalization util — no raw URLs sent.
- [ ] Error boundary captures `error_type`, `source`, `component_stack_depth` only.
- [ ] No `exception_message` or `stack_trace` flowing from any backend code path.
- [ ] BG job catch blocks emit `background_job_failed` with `job_name` + `error_type` only.
- [ ] `release_sha` populated from CI in deployed builds.

## Phase 1 deferred items

This guide will be expanded in Phase 4 / 7 with concrete SDK install snippets and the third-app onboarding checklist (`docs/onboarding-checklist.md`).
