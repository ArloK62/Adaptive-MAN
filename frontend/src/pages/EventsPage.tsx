import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { api } from '../lib/api';
import type { EventRowDto } from '../lib/api';
import { resolveRange, useFilters } from '../lib/filters';
import { Pager } from './ErrorsPage';

const PAGE_SIZE = 50;

export function EventsPage() {
  const { filters, ready } = useFilters();
  const range = resolveRange(filters);
  const [params] = useSearchParams();

  const initialEventName = params.get('event_name') ?? '';
  const [eventName, setEventName] = useState(initialEventName);
  const [distinctId, setDistinctId] = useState('');
  const [correlationId, setCorrelationId] = useState('');
  const [page, setPage] = useState(0);
  const [selected, setSelected] = useState<EventRowDto | null>(null);

  const { data, isLoading, isError } = useQuery({
    enabled: ready,
    queryKey: ['events', filters.app, filters.env, range.from, range.to, eventName, distinctId, correlationId, page],
    queryFn: () => api.events({
      app: filters.app, env: filters.env, from: range.from, to: range.to,
      page, pageSize: PAGE_SIZE,
      event_name: eventName || undefined,
      distinct_id: distinctId || undefined,
      correlation_id: correlationId || undefined,
    }),
  });

  if (!ready) return <div className="p-6 text-sm text-slate-500">Pick an app + environment.</div>;

  return (
    <div className="p-6">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-800">Events</h2>
        <button
          className="rounded border bg-white px-3 py-1.5 text-xs font-medium text-slate-700 shadow-sm hover:bg-slate-50 disabled:opacity-50"
          onClick={() => exportCsv(data?.rows ?? [])}
          disabled={!data?.rows.length}
        >
          Export CSV (current page)
        </button>
      </div>

      <div className="mb-3 flex flex-wrap gap-2">
        <Input placeholder="event_name" value={eventName} onChange={(v) => { setEventName(v); setPage(0); }} />
        <Input placeholder="distinct_id" value={distinctId} onChange={(v) => { setDistinctId(v); setPage(0); }} />
        <Input placeholder="correlation_id" value={correlationId} onChange={(v) => { setCorrelationId(v); setPage(0); }} />
      </div>

      <div className="overflow-hidden rounded-lg border bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wider text-slate-500">
            <tr><Th>Time</Th><Th>Event</Th><Th>Distinct ID</Th><Th>Route</Th><Th>Feature</Th><Th>Release</Th></tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={6} className="p-6 text-center text-slate-400">Loading…</td></tr>}
            {isError && <tr><td colSpan={6} className="p-6 text-center text-rose-600">Failed to load.</td></tr>}
            {data?.rows.length === 0 && <tr><td colSpan={6} className="p-6 text-center text-slate-400">No events match.</td></tr>}
            {data?.rows.map((r) => (
              <tr key={r.id} className="cursor-pointer border-t hover:bg-slate-50" onClick={() => setSelected(r)}>
                <Td>{new Date(r.created_at).toLocaleString()}</Td>
                <Td className="font-medium">{r.event_name}</Td>
                <Td className="font-mono text-xs">{r.distinct_id}</Td>
                <Td className="text-xs text-slate-600">{r.normalized_route ?? r.endpoint_group ?? '—'}</Td>
                <Td className="text-xs text-slate-500">{r.feature_area ?? '—'}</Td>
                <Td className="text-xs text-slate-500">{r.release_sha ?? '—'}</Td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Pager page={page} pageSize={PAGE_SIZE} total={data?.total ?? 0} onChange={setPage} />

      {selected && <EventDetail row={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) { return <th className="px-3 py-2">{children}</th>; }
function Td({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <td className={`px-3 py-2 ${className}`}>{children}</td>;
}

function Input({ placeholder, value, onChange }: { placeholder: string; value: string; onChange: (v: string) => void }) {
  return (
    <input
      placeholder={placeholder}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="rounded border px-3 py-1.5 text-sm placeholder:text-slate-400"
    />
  );
}

function EventDetail({ row, onClose }: { row: EventRowDto; onClose: () => void }) {
  let pretty = row.properties_json;
  try { pretty = JSON.stringify(JSON.parse(row.properties_json), null, 2); } catch { /* keep raw */ }
  return (
    <div className="fixed inset-0 z-30 flex justify-end bg-black/30" onClick={onClose}>
      <div className="h-full w-full max-w-lg overflow-y-auto bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between">
          <div>
            <div className="text-xs uppercase tracking-wider text-slate-500">Event</div>
            <h3 className="text-lg font-semibold text-slate-800">{row.event_name}</h3>
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600" aria-label="Close">✕</button>
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-2 text-xs">
          <Field label="Distinct ID" value={row.distinct_id} />
          <Field label="Session ID" value={row.session_id} />
          <Field label="Correlation ID" value={row.correlation_id} />
          <Field label="Endpoint group" value={row.endpoint_group} />
          <Field label="Normalized route" value={row.normalized_route} />
          <Field label="Feature area" value={row.feature_area} />
          <Field label="Release SHA" value={row.release_sha} />
          <Field label="Occurred at" value={new Date(row.occurred_at).toLocaleString()} />
        </dl>
        <h4 className="mt-4 text-xs font-semibold uppercase tracking-wider text-slate-500">Properties</h4>
        <pre className="mt-1 max-h-96 overflow-auto rounded bg-slate-900 p-3 text-xs text-slate-100">{pretty}</pre>
      </div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <>
      <dt className="text-slate-500">{label}</dt>
      <dd className="truncate text-right font-mono text-slate-800">{value ?? '—'}</dd>
    </>
  );
}

function exportCsv(rows: EventRowDto[]) {
  if (!rows.length) return;
  const cols: (keyof EventRowDto)[] = [
    'created_at', 'occurred_at', 'event_name', 'distinct_id', 'session_id',
    'correlation_id', 'normalized_route', 'endpoint_group', 'feature_area',
    'release_sha', 'properties_json',
  ];
  const csv = [cols.join(',')]
    .concat(rows.map((r) => cols.map((c) => csvEscape(r[c])).join(',')))
    .join('\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `events-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.csv`;
  document.body.appendChild(a); a.click(); a.remove();
  URL.revokeObjectURL(url);
}

function csvEscape(v: unknown): string {
  if (v === null || v === undefined) return '';
  const s = String(v);
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}
