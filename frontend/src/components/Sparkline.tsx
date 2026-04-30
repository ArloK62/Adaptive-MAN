import { Line, LineChart, ResponsiveContainer } from 'recharts';
import type { SparklinePoint } from '../lib/api';

export function Sparkline({ data, stroke = '#0ea5e9' }: { data: SparklinePoint[] | undefined; stroke?: string }) {
  if (!data || data.length === 0) {
    return <div className="flex h-full items-center text-xs text-slate-400">no data</div>;
  }
  return (
    <ResponsiveContainer width="100%" height="100%">
      <LineChart data={data} margin={{ top: 2, right: 0, bottom: 2, left: 0 }}>
        <Line type="monotone" dataKey="c" stroke={stroke} strokeWidth={2} dot={false} isAnimationActive={false} />
      </LineChart>
    </ResponsiveContainer>
  );
}
