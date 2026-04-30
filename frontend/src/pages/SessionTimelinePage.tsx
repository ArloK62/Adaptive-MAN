import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api, type TimelineEntry } from '../lib/api';

export function SessionTimelinePage() {
  const { sessionId = '' } = useParams<{ sessionId: string }>();
  const [errorsOnly, setErrorsOnly] = useState(false);
  const [selected, setSelected] = useState<TimelineEntry | null>(null);

  const { data, isLoading, error } = useQuery({
    enabled: !!sessionId,
    queryKey: ['session-timeline', sessionId],
    queryFn: () => api.sessionTimeline(sessionId),
  });

  if (isLoading) return <div className="p-6 text-sm text-slate-500">Loading…</div>;
  if (error) return <div className="p-6 text-sm text-rose-600">Failed to load timeline.</div>;
  if (!data) return null;

  const entries = errorsOnly ? data.entries.filter((e) => e.kind === 'error' || e.is_api_failure === true) : data.entries;
  const sessionEnd = data.session.ended_at;

  return (
    <div className="p-6">
      <div className="mb-3 flex items-center gap-3">
        <Link to="/sessions" className="text-xs text-slate-500 hover:underline">← Sessions</Link>
        <h2 className="text-lg font-semibold text-slate-800">Session timeline</h2>
      </div>

      <div className="mb-4 rounded border border-slate-200 bg-white p-4 text-sm">
        <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
          <Field label="Session ID" value={data.session.session_id} mono />
          <Field label="User" value={data.session.distinct_id} />
          <Field label="Started" value={new Date(data.session.started_at).toLocaleString()} />
          <Field label="Ended" value={sessionEnd ? new Date(sessionEnd).toLocaleString() : '—'} />
          <Field label="Last seen" value={new Date(data.session.last_seen_at).toLocaleString()} />
          <Field label="Has error" value={data.session.has_error ? 'yes' : 'no'} />
          <Field label="Release" value={data.session.release_sha ?? '—'} mono />
        </div>
      </div>

      <label className="mb-3 flex cursor-pointer items-center gap-2 text-sm text-slate-600">
        <input
          type="checkbox"
          checked={errorsOnly}
          onChange={(e) => setErrorsOnly(e.target.checked)}
          className="h-4 w-4 rounded border-slate-300"
        />
        Errors only
      </label>

      <div className="grid grid-cols-12 gap-4">
        <div className="col-span-12 lg:col-span-8">
          <ol className="relative border-l-2 border-slate-200 pl-6">
            {entries.length === 0 && (
              <li className="text-sm text-slate-400">No entries match.</li>
            )}
            {entries.map((entry, idx) => (
              <li
                key={`${entry.kind}-${entry.id}-${idx}`}
                className={`mb-4 cursor-pointer rounded border bg-white p-3 text-sm shadow-sm transition hover:shadow ${
                  selected === entry ? 'border-indigo-300 ring-1 ring-indigo-200' : 'border-slate-200'
                }`}
                onClick={() => setSelected(entry)}
              >
                <span
                  className={`absolute -left-[7px] mt-2 h-3 w-3 rounded-full border-2 border-white ${dotColor(entry)}`}
                />
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <KindBadge entry={entry} />
                    <span className="font-mono text-xs text-slate-500">
                      {new Date(entry.occurred_at).toLocaleTimeString()}
                    </span>
                  </div>
                  {'correlation_id' in entry && entry.correlation_id && (
                    <span className="font-mono text-[10px] text-slate-400">{entry.correlation_id}</span>
                  )}
                </div>
                <div className="mt-1 font-medium text-slate-800">{summary(entry)}</div>
              </li>
            ))}
          </ol>
        </div>

        <div className="col-span-12 lg:col-span-4">
          <div className="sticky top-2 rounded border border-slate-200 bg-white p-4 text-sm">
            <h3 className="mb-2 font-semibold text-slate-700">Details</h3>
            {selected ? (
              <pre className="whitespace-pre-wrap break-words text-xs text-slate-700">
                {JSON.stringify(selected, null, 2)}
              </pre>
            ) : (
              <p className="text-xs text-slate-400">Click an entry to inspect.</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-slate-400">{label}</div>
      <div className={mono ? 'font-mono text-xs text-slate-700' : 'text-slate-700'}>{value}</div>
    </div>
  );
}

function KindBadge({ entry }: { entry: TimelineEntry }) {
  if (entry.kind === 'error') {
    const cls = entry.source === 'cross_process' ? 'bg-purple-100 text-purple-700' : 'bg-rose-100 text-rose-700';
    return <span className={`rounded px-2 py-0.5 text-[10px] uppercase tracking-wide ${cls}`}>{entry.source === 'cross_process' ? 'be error' : 'error'}</span>;
  }
  if (entry.is_api_failure) {
    return <span className="rounded bg-amber-100 px-2 py-0.5 text-[10px] uppercase tracking-wide text-amber-700">api failure</span>;
  }
  return <span className="rounded bg-slate-100 px-2 py-0.5 text-[10px] uppercase tracking-wide text-slate-600">event</span>;
}

function dotColor(entry: TimelineEntry): string {
  if (entry.kind === 'error') return entry.source === 'cross_process' ? 'bg-purple-500' : 'bg-rose-500';
  if (entry.is_api_failure) return 'bg-amber-500';
  return 'bg-slate-400';
}

function summary(entry: TimelineEntry): string {
  if (entry.kind === 'event') {
    return entry.normalized_route ? `${entry.event_name} — ${entry.normalized_route}` : entry.event_name;
  }
  const parts = [entry.error_type];
  if (entry.exception_type) parts.push(entry.exception_type);
  if (entry.endpoint_group) parts.push(entry.endpoint_group);
  if (entry.http_status_code) parts.push(String(entry.http_status_code));
  return parts.join(' • ');
}
