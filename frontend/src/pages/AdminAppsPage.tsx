import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';

export function AdminAppsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['apps'], queryFn: api.apps });

  return (
    <div className="p-6">
      <h2 className="mb-4 text-lg font-semibold text-slate-800">Apps</h2>
      <p className="mb-4 text-sm text-slate-500">
        Read-only inventory for now. Onboarding (create app, mint API keys) is part of a later phase.
      </p>

      {isLoading && <div className="text-sm text-slate-400">Loading…</div>}
      {isError && <div className="text-sm text-rose-600">Failed to load apps.</div>}

      <div className="space-y-3">
        {data?.map((a) => (
          <div key={a.id} className="rounded-lg border bg-white p-4 shadow-sm">
            <div className="flex items-baseline justify-between">
              <div>
                <div className="text-sm font-semibold text-slate-800">{a.name}</div>
                <div className="font-mono text-xs text-slate-500">{a.slug}</div>
              </div>
              <div className="text-xs text-slate-400">{a.id}</div>
            </div>
            {a.description && <p className="mt-1 text-sm text-slate-600">{a.description}</p>}
            <div className="mt-3 flex flex-wrap gap-2">
              {a.environments.map((e) => (
                <span key={e.id} className="rounded-full border bg-slate-50 px-2 py-0.5 text-xs text-slate-600">
                  {e.name}
                </span>
              ))}
              {a.environments.length === 0 && <span className="text-xs text-slate-400">no environments</span>}
            </div>
          </div>
        ))}
        {data?.length === 0 && <div className="text-sm text-slate-400">No apps registered.</div>}
      </div>
    </div>
  );
}
