import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { useFilters } from '../lib/filters';

export function SessionsPage() {
  const { filters, ready } = useFilters();
  const { data } = useQuery({
    enabled: ready,
    queryKey: ['sessions', filters.app, filters.env],
    queryFn: () => api.sessions({ app: filters.app, env: filters.env }),
  });

  return (
    <div className="p-6">
      <h2 className="mb-2 text-lg font-semibold text-slate-800">Sessions</h2>
      <p className="text-sm text-slate-500">
        Session capture lands in Phase 5. The endpoint returns an empty result today.
      </p>
      {data?.note && <p className="mt-2 text-xs text-slate-400">{data.note}</p>}
    </div>
  );
}
