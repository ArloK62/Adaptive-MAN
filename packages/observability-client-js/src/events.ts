/**
 * Compile-time event allowlist for frontend-emitted Phase 1 events.
 * Server-side EventCatalog (backend/src/Observability.Application/Ingestion/EventCatalog.cs)
 * is the runtime source of truth; this mirrors the FE-relevant subset.
 *
 * Backend-only events (server_error_occurred, background_job_failed) are intentionally absent.
 */

export type AuthLoginSuccessProps = {
  generic_role?: string;
  release_sha?: string;
};

export type AuthLogoutProps = {
  release_sha?: string;
};

export type PageViewedProps = {
  normalized_route: string;
  feature_area?: string;
  release_sha?: string;
};

export type ApiRequestFailedProps = {
  endpoint_group: string;
  method: string;
  http_status_code: number;
  is_network_error: boolean;
  correlation_id?: string;
  release_sha?: string;
};

export type FrontendExceptionProps = {
  error_type: string;
  source: string;
  component_stack_depth?: number;
  normalized_route?: string;
  release_sha?: string;
};

export type DevSmokeTestProps = {
  release_sha?: string;
};

export type EventMap = {
  auth_login_success: AuthLoginSuccessProps;
  auth_logout: AuthLogoutProps;
  page_viewed: PageViewedProps;
  api_request_failed: ApiRequestFailedProps;
  frontend_exception: FrontendExceptionProps;
  dev_smoke_test: DevSmokeTestProps;
};

export type EventName = keyof EventMap;
export type PropsFor<E extends EventName> = EventMap[E];
