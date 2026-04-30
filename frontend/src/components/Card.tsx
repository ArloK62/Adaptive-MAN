import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';

interface CardProps {
  title: string;
  total: number | string;
  to?: string;
  children?: ReactNode;
}

export function Card({ title, total, to, children }: CardProps) {
  const inner = (
    <div className="rounded-lg border bg-white p-4 shadow-sm transition hover:shadow-md">
      <div className="text-xs font-medium uppercase tracking-wider text-slate-500">{title}</div>
      <div className="mt-1 text-2xl font-semibold tabular-nums text-slate-900">{total}</div>
      {children && <div className="mt-3 h-12">{children}</div>}
    </div>
  );
  return to ? <Link to={to}>{inner}</Link> : inner;
}
