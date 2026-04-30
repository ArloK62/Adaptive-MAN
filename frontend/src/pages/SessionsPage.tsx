import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useFilters } from '../lib/filters';

export function SessionsPage() {
  const { filters, ready } = useFilters();
  const [errorsOnly, setErrorsOnly] = useState(false);
  const { data, isLoading, error } = useQuery({
    enabled: ready,
    queryKey: ['sessions', filters.app, filters.env, filters.from, filters.to, errorsOnly],
    queryFn: () =>
      api.sessions({
        app: filters.app,
        env: filters.env,
        from: filters.from,
        to: filters.to,
        errors_only: errorsOnly || undefined,
      }),
  });

  return (
    <div className="p-6">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-800">Sessions</h2>
        <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={errorsOnly}
            onChange={(e) => setErrorsOnly(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Errors only
        </label>
      </div>

      {error && <p className="text-sm text-rose-600">Failed to load sessions.</p>}
      {isLoading && <p className="text-sm text-slate-500">Loading…</p>}

      {data && (
        <div className="overflow-hidden rounded border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-3 py-2">Session</th>
                <th className="px-3 py-2">User</th>
                <th className="px-3 py-2">Started</th>
                <th className="px-3 py-2">Last seen</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Release</th>
              </tr>
            </thead>
            <tbody>
              {data.rows.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-3 py-6 text-center text-slate-400">
                    No sessions in range.
                  </td>
                </tr>
              )}
              {data.rows.map((s) => (
                <tr key={s.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-3 py-2 font-mono text-xs">
                    <Link to={`/sessions/${encodeURIComponent(s.session_id)}`} className="text-indigo-600 hover:underline">
                      {s.session_id}
                    </Link>
                  </td>
                  <td className="px-3 py-2">{s.distinct_id}</td>
                  <td className="px-3 py-2 text-slate-500">{new Date(s.started_at).toLocaleString()}</td>
                  <td className="px-3 py-2 text-slate-500">{new Date(s.last_seen_at).toLocaleString()}</td>
                  <td className="px-3 py-2">
                    {s.has_error ? (
                      <span className="rounded bg-rose-100 px-2 py-0.5 text-xs text-rose-700">error</span>
                    ) : s.ended_at ? (
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">ended</span>
                    ) : (
                      <span className="rounded bg-emerald-100 px-2 py-0.5 text-xs text-emerald-700">active</span>
                    )}
                  </td>
                  <td className="px-3 py-2 font-mono text-xs text-slate-500">{s.release_sha ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="border-t border-slate-100 px-3 py-2 text-xs text-slate-500">
            {data.total} total
          </div>
        </div>
      )}
    </div>
  );
}
