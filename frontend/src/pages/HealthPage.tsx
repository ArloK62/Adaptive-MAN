import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { resolveRange, useFilters } from '../lib/filters';
import { Card } from '../components/Card';
import { Sparkline } from '../components/Sparkline';

export function HealthPage() {
  const { filters, ready } = useFilters();
  const range = resolveRange(filters);

  const { data, isLoading, isError, error } = useQuery({
    enabled: ready,
    queryKey: ['health', filters.app, filters.env, range.from, range.to],
    queryFn: () => api.health({ app: filters.app, env: filters.env, from: range.from, to: range.to }),
    refetchInterval: 30_000,
  });

  if (!ready) return <Empty>Pick an app + environment to view health.</Empty>;
  if (isLoading) return <Empty>Loading…</Empty>;
  if (isError) return <Empty>Failed to load: {(error as Error).message}</Empty>;
  if (!data) return null;

  const c = data.cards;

  return (
    <div className="p-6">
      <h2 className="mb-4 text-lg font-semibold text-slate-800">Health</h2>
      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
        <Card title="Backend 500s" total={c.backend_500s} to="/errors">
          <Sparkline data={data.sparklines['server_error_occurred']} stroke="#dc2626" />
        </Card>
        <Card title="Frontend exceptions" total={c.frontend_exceptions} to="/errors">
          <Sparkline data={data.sparklines['frontend_exception']} stroke="#ea580c" />
        </Card>
        <Card title="API request failures" total={c.api_request_failures} to="/events?event_name=api_request_failed">
          <Sparkline data={data.sparklines['api_request_failed']} stroke="#d97706" />
        </Card>
        <Card title="BG job failures" total={c.background_job_failures} to="/errors">
          <Sparkline data={data.sparklines['background_job_failed']} stroke="#b45309" />
        </Card>
        <Card title="Page views" total={c.page_views} to="/events?event_name=page_viewed">
          <Sparkline data={data.sparklines['page_viewed']} />
        </Card>
        <Card title="Logins" total={c.logins} to="/events?event_name=auth_login_success">
          <Sparkline data={data.sparklines['auth_login_success']} stroke="#059669" />
        </Card>
      </div>

      <div className="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-3">
        <RankList title="Page views by feature" items={data.page_views_by_feature.map((p) => ({ label: p.feature, value: p.count }))} />
        <RankList title="Top failing endpoint groups" items={data.top_failing_endpoint_groups.map((p) => ({ label: p.endpoint_group, value: p.occurrences }))} />
        <RankList title="Errors by release" items={data.errors_by_release.map((p) => ({ label: p.release, value: p.occurrences }))} />
      </div>
    </div>
  );
}

function RankList({ title, items }: { title: string; items: { label: string; value: number }[] }) {
  return (
    <div className="rounded-lg border bg-white p-4 shadow-sm">
      <div className="text-xs font-medium uppercase tracking-wider text-slate-500">{title}</div>
      {items.length === 0 && <div className="mt-2 text-xs text-slate-400">no data</div>}
      <ul className="mt-2 space-y-1 text-sm">
        {items.map((i) => (
          <li key={i.label} className="flex justify-between">
            <span className="truncate pr-2 text-slate-700">{i.label}</span>
            <span className="tabular-nums text-slate-500">{i.value}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function Empty({ children }: { children: React.ReactNode }) {
  return <div className="p-6 text-sm text-slate-500">{children}</div>;
}
