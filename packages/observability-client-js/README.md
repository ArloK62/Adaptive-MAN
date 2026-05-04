# @adaptive/observability-client-js

Frontend SDK for the Adaptive Observability platform.

The public surface mirrors `sch-ui/src/services/analytics.ts` so the SCH PostHog → Adaptive cutover (Phase 6) is import-line + DI-swap only.

## Install

```bash
npm install @adaptive/observability-client-js
```

## Quickstart (under 50 LOC)

```ts
// src/main.tsx (or wherever your app boots)
import * as observability from "@adaptive/observability-client-js";

observability.init({
  ingestUrl: import.meta.env.VITE_OBSERVABILITY_URL!,
  apiKey: import.meta.env.VITE_OBSERVABILITY_KEY!,
  environment: import.meta.env.MODE,
  releaseSha: import.meta.env.VITE_RELEASE_SHA,
});

// On login:
observability.identify(String(userId)); // string only — caller is responsible for safety

// Page views (call from your router):
observability.capturePageView(location.pathname);

// Auth events:
observability.track("auth_login_success", { generic_role: "clinician" });
observability.track("auth_logout");
```

See [`docs/privacy-rules.md`](../../docs/privacy-rules.md) for what you may NOT send.

## Optional: Axios interceptor

```ts
import axios from "axios";
import { attachAxiosInterceptor } from "@adaptive/observability-client-js/axios";

const api = axios.create({ baseURL: "/api" });
attachAxiosInterceptor(api);
```

Captures `endpoint_group`, `method`, `http_status_code`, `is_network_error`, and `correlation_id` (read from `x-correlation-id` response header) on every failure.

## Optional: React error boundary

```tsx
import { ObservabilityErrorBoundary } from "@adaptive/observability-client-js/react";

<ObservabilityErrorBoundary fallback={<p>Something went wrong.</p>}>
  <App />
</ObservabilityErrorBoundary>
```

NEVER sends `error.message`, `error.stack`, or React `componentStack` text. Only `error_type`, `source`, `component_stack_depth`.

## Replay slot (Phase 9)

Phase 4 ships only the no-op adapter and the type contract — no `rrweb` dependency yet. Phase 9 will drop in an rrweb-backed adapter at `@adaptive/observability-client-js/replay` without breaking SemVer.

## API surface

| Function | Notes |
|---|---|
| `init(options)` | Idempotent; calling with `enabled: false` is a no-op. Set `trackSessions: false` to opt out of automatic `/sessions/start` + `/sessions/end` calls. |
| `identify(distinctId)` | String only. No `user_` prefix per platform identity rules. |
| `track(event, props)` | Compile-time event allowlist (TS unions) per `events.ts`. |
| `capturePageView(path?, featureArea?)` | Auto-normalizes route. |
| `captureException({ errorType, source, componentStackDepth, normalizedRoute })` | Never accepts message/stack text. |
| `captureFailedRequest({ url, method, httpStatusCode, isNetworkError, correlationId })` | |
| `flush()` | Force-send pending batch. |
| `shutdown()` | Drains transport, stops replay adapter, and sends `/sessions/end`. |
| `getSessionId()` | The shared id used by replay (Phase 9) and session timeline (Phase 5). |
| `reset()` | New session id, clear distinct id (call on logout). |

## PostHog migration cheatsheet

| PostHog (current SCH) | Adaptive (this SDK) |
|---|---|
| `posthog.init(key, { api_host })` | `observability.init({ ingestUrl, apiKey })` |
| `posthog.identify(String(userId))` | `observability.identify(String(userId))` |
| `posthog.capture("event", props)` | `observability.track("event", props)` |
| `posthog.reset()` | `observability.reset()` |
| Manual page view | `observability.capturePageView()` |

Event names, identity rules, and allowed property shapes are unchanged from `POSTHOG_EVENT_CATALOG.md`.
