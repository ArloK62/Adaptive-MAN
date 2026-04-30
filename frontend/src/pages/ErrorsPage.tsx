import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import type { ErrorRowDto } from '../lib/api';
import { resolveRange, useFilters } from '../lib/filters';

const PAGE_SIZE = 50;

export function ErrorsPage() {
  const { filters, ready } = useFilters();
  const range = resolveRange(filters);
  const [page, setPage] = useState(0);
  const [sort, setSort] = useState<'last_seen_at' | 'occurrence_count'>('last_seen_at');
  const [selected, setSelected] = useState<ErrorRowDto | null>(null);

  const { data, isLoading, isError } = useQuery({
    enabled: ready,
    queryKey: ['errors', filters.app, filters.env, range.from, range.to, page, sort],
    queryFn: () => api.errors({
      app: filters.app, env: filters.env, from: range.from, to: range.to,
      page, pageSize: PAGE_SIZE, sort,
    }),
  });

  if (!ready) return <div className="p-6 text-sm text-slate-500">Pick an app + environment.</div>;

  return (
    <div className="p-6">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-800">Errors</h2>
        <div className="text-xs text-slate-500">
          Sort:
          <select
            className="ml-2 rounded border px-2 py-1 text-xs"
            value={sort}
            onChange={(e) => { setSort(e.target.value as typeof sort); setPage(0); }}
          >
            <option value="last_seen_at">Last seen</option>
            <option value="occurrence_count">Occurrence count</option>
          </select>
        </div>
      </div>

      <div className="overflow-hidden rounded-lg border bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wider text-slate-500">
            <tr>
              <Th>Type</Th><Th>Route / Job</Th><Th>Count</Th><Th>Last seen</Th><Th>Release</Th><Th>Status</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={6} className="p-6 text-center text-slate-400">Loading…</td></tr>}
            {isError && <tr><td colSpan={6} className="p-6 text-center text-rose-600">Failed to load.</td></tr>}
            {data?.rows.length === 0 && <tr><td colSpan={6} className="p-6 text-center text-slate-400">No errors in range.</td></tr>}
            {data?.rows.map((r) => (
              <tr key={r.id} className="cursor-pointer border-t hover:bg-slate-50" onClick={() => setSelected(r)}>
                <Td className="font-medium text-slate-800">{r.error_type}</Td>
                <Td className="text-slate-600">{r.endpoint_group ?? r.job_name ?? r.normalized_route ?? '—'}</Td>
                <Td className="tabular-nums">{r.occurrence_count}</Td>
                <Td>{new Date(r.last_seen_at).toLocaleString()}</Td>
                <Td className="text-xs text-slate-500">{r.release_sha ?? '—'}</Td>
                <Td>{r.http_status_code ?? '—'}</Td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Pager page={page} pageSize={PAGE_SIZE} total={data?.total ?? 0} onChange={setPage} />

      {selected && <ErrorDetailDrawer row={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) { return <th className="px-3 py-2">{children}</th>; }
function Td({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <td className={`px-3 py-2 ${className}`}>{children}</td>;
}

export function Pager({ page, pageSize, total, onChange }: { page: number; pageSize: number; total: number; onChange: (p: number) => void }) {
  const last = Math.max(0, Math.ceil(total / pageSize) - 1);
  return (
    <div className="mt-3 flex items-center justify-between text-xs text-slate-500">
      <div>{total} total · page {page + 1} / {last + 1}</div>
      <div className="flex gap-2">
        <button className="rounded border px-2 py-1 disabled:opacity-40" disabled={page === 0} onClick={() => onChange(page - 1)}>Prev</button>
        <button className="rounded border px-2 py-1 disabled:opacity-40" disabled={page >= last} onClick={() => onChange(page + 1)}>Next</button>
      </div>
    </div>
  );
}

function ErrorDetailDrawer({ row, onClose }: { row: ErrorRowDto; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-30 flex justify-end bg-black/30" onClick={onClose}>
      <div className="h-full w-full max-w-md overflow-y-auto bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between">
          <div>
            <div className="text-xs uppercase tracking-wider text-slate-500">Error</div>
            <h3 className="text-lg font-semibold text-slate-800">{row.error_type}</h3>
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600" aria-label="Close">✕</button>
        </div>
        <dl className="mt-4 space-y-2 text-sm">
          <Row label="Fingerprint" value={row.fingerprint} />
          <Row label="Exception type" value={row.exception_type} />
          <Row label="Endpoint group" value={row.endpoint_group} />
          <Row label="Job name" value={row.job_name} />
          <Row label="Normalized route" value={row.normalized_route} />
          <Row label="HTTP status" value={row.http_status_code?.toString()} />
          <Row label="Release SHA" value={row.release_sha} />
          <Row label="First seen" value={new Date(row.first_seen_at).toLocaleString()} />
          <Row label="Last seen" value={new Date(row.last_seen_at).toLocaleString()} />
          <Row label="Occurrences" value={row.occurrence_count.toString()} />
          <Row label="Last correlation ID" value={row.last_correlation_id} />
        </dl>
        <p className="mt-4 text-xs text-slate-400">
          Per privacy rules, no exception messages or stack traces are stored or shown.
        </p>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="flex justify-between gap-4 border-b border-slate-100 pb-1">
      <dt className="text-slate-500">{label}</dt>
      <dd className="truncate text-right font-mono text-xs text-slate-800">{value ?? '—'}</dd>
    </div>
  );
}
