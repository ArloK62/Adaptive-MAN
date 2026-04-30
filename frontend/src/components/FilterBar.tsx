import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useFilters } from '../lib/filters';
import type { RangePreset } from '../lib/filters';

const PRESETS: { value: RangePreset; label: string }[] = [
  { value: '1h', label: 'Last 1h' },
  { value: '24h', label: 'Last 24h' },
  { value: '7d', label: 'Last 7d' },
  { value: '30d', label: 'Last 30d' },
  { value: 'custom', label: 'Custom' },
];

export function FilterBar() {
  const { filters, setFilters } = useFilters();
  const appsQuery = useQuery({ queryKey: ['apps'], queryFn: api.apps });

  // Auto-select first app + first env if nothing chosen yet.
  useEffect(() => {
    if (!appsQuery.data || appsQuery.data.length === 0) return;
    if (!filters.app) {
      const first = appsQuery.data[0];
      setFilters({ app: first.id, env: first.environments[0]?.id ?? '' });
    } else if (!filters.env) {
      const app = appsQuery.data.find((a) => a.id === filters.app);
      if (app) setFilters({ env: app.environments[0]?.id ?? '' });
    }
  }, [appsQuery.data, filters.app, filters.env, setFilters]);

  const selectedApp = appsQuery.data?.find((a) => a.id === filters.app);

  return (
    <div className="flex flex-wrap items-center gap-3 border-b bg-white px-6 py-3">
      <Field label="App">
        <select
          className="rounded border px-2 py-1 text-sm"
          value={filters.app}
          onChange={(e) => {
            const next = appsQuery.data?.find((a) => a.id === e.target.value);
            setFilters({ app: e.target.value, env: next?.environments[0]?.id ?? '' });
          }}
          disabled={appsQuery.isLoading || !appsQuery.data?.length}
        >
          {!appsQuery.data?.length && <option>{appsQuery.isLoading ? 'loading…' : 'no apps'}</option>}
          {appsQuery.data?.map((a) => (
            <option key={a.id} value={a.id}>{a.name}</option>
          ))}
        </select>
      </Field>

      <Field label="Env">
        <select
          className="rounded border px-2 py-1 text-sm"
          value={filters.env}
          onChange={(e) => setFilters({ env: e.target.value })}
          disabled={!selectedApp}
        >
          {!selectedApp?.environments.length && <option>—</option>}
          {selectedApp?.environments.map((e) => (
            <option key={e.id} value={e.id}>{e.name}</option>
          ))}
        </select>
      </Field>

      <Field label="Range">
        <select
          className="rounded border px-2 py-1 text-sm"
          value={filters.range}
          onChange={(e) => setFilters({ range: e.target.value as RangePreset })}
        >
          {PRESETS.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
        </select>
      </Field>

      {filters.range === 'custom' && (
        <>
          <Field label="From">
            <input
              type="datetime-local"
              className="rounded border px-2 py-1 text-sm"
              value={toLocalInput(filters.from)}
              onChange={(e) => setFilters({ from: fromLocalInput(e.target.value) })}
            />
          </Field>
          <Field label="To">
            <input
              type="datetime-local"
              className="rounded border px-2 py-1 text-sm"
              value={toLocalInput(filters.to)}
              onChange={(e) => setFilters({ to: fromLocalInput(e.target.value) })}
            />
          </Field>
        </>
      )}

      <div className="ml-auto text-xs text-slate-500">
        {appsQuery.isError && <span className="text-rose-600">Backend unreachable</span>}
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex items-center gap-2 text-xs uppercase tracking-wider text-slate-500">
      {label}
      {children}
    </label>
  );
}

function toLocalInput(iso?: string): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
function fromLocalInput(value: string): string | undefined {
  return value ? new Date(value).toISOString() : undefined;
}
