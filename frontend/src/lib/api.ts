// Frontend API client. Phase 8 will add auth headers; for now the dashboard runs against an
// internal-only backend without auth.

const RAW_BASE = ((import.meta as unknown as { env?: Record<string, string> }).env?.VITE_OBSERVABILITY_API_URL) ?? 'http://localhost:8080';
export const API_BASE = RAW_BASE.replace(/\/$/, '');

export interface AppEnvironmentDto {
  id: string;
  name: string;
}

export interface AppDto {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  environments: AppEnvironmentDto[];
}

export interface HealthCardsDto {
  backend_500s: number;
  frontend_exceptions: number;
  api_request_failures: number;
  background_job_failures: number;
  page_views: number;
  logins: number;
}

export interface SparklinePoint {
  t: string;
  c: number;
}

export interface HealthDto {
  range: { from: string; to: string };
  cards: HealthCardsDto;
  by_event: { name: string; count: number }[];
  page_views_by_feature: { feature: string; count: number }[];
  top_failing_endpoint_groups: { endpoint_group: string; occurrences: number }[];
  errors_by_release: { release: string; occurrences: number }[];
  sparklines: Record<string, SparklinePoint[]>;
}

export interface ErrorRowDto {
  id: number;
  fingerprint: string;
  error_type: string;
  exception_type: string | null;
  endpoint_group: string | null;
  job_name: string | null;
  normalized_route: string | null;
  http_status_code: number | null;
  release_sha: string | null;
  occurrence_count: number;
  first_seen_at: string;
  last_seen_at: string;
  last_correlation_id: string | null;
}

export interface EventRowDto {
  id: number;
  event_name: string;
  distinct_id: string;
  session_id: string | null;
  correlation_id: string | null;
  normalized_route: string | null;
  endpoint_group: string | null;
  feature_area: string | null;
  release_sha: string | null;
  occurred_at: string;
  created_at: string;
  properties_json: string;
}

export interface PagedResult<T> {
  total: number;
  page: number;
  page_size: number;
  rows: T[];
}

export class ApiError extends Error {
  status: number;
  body: unknown;
  constructor(message: string, status: number, body: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { Accept: 'application/json', ...(init?.headers ?? {}) },
  });
  if (!res.ok) {
    let body: unknown = null;
    try { body = await res.json(); } catch { /* ignore */ }
    throw new ApiError(`Request failed: ${res.status}`, res.status, body);
  }
  return res.json() as Promise<T>;
}

type AnyQuery = Record<string, string | number | undefined>;

export const api = {
  apps: () => request<AppDto[]>('/api/apps'),
  health: (q: DashboardQuery) => request<HealthDto>(`/api/dashboard/health${buildQuery(q as unknown as AnyQuery)}`),
  errors: (q: DashboardQuery & PagingQuery & { sort?: string }) =>
    request<PagedResult<ErrorRowDto>>(`/api/dashboard/errors${buildQuery(q as unknown as AnyQuery)}`),
  events: (q: DashboardQuery & PagingQuery & EventFilters) =>
    request<PagedResult<EventRowDto>>(`/api/dashboard/events${buildQuery(q as unknown as AnyQuery)}`),
  sessions: (q: DashboardQuery & PagingQuery) =>
    request<PagedResult<unknown> & { note?: string }>(`/api/dashboard/sessions${buildQuery(q as unknown as AnyQuery)}`),
};

export interface DashboardQuery {
  app: string;
  env: string;
  from?: string;
  to?: string;
}

export interface PagingQuery { page?: number; pageSize?: number }
export interface EventFilters {
  event_name?: string;
  distinct_id?: string;
  correlation_id?: string;
}

function buildQuery(q: Record<string, string | number | undefined>): string {
  const parts: string[] = [];
  for (const [k, v] of Object.entries(q)) {
    if (v === undefined || v === '' || v === null) continue;
    const key = k === 'pageSize' ? 'pageSize' : k;
    parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(String(v))}`);
  }
  return parts.length ? `?${parts.join('&')}` : '';
}
