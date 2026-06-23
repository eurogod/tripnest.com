import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { EmptyState, ErrorState, Skeleton } from './ui';

/* ---------- Page header ---------- */
export function PageHeader({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="text-2xl font-extrabold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 max-w-2xl text-sm text-muted">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

/* ---------- Stat cards ---------- */
export function StatGrid({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">{children}</div>;
}

export function StatCard({
  label,
  value,
  icon,
  hint,
  tone = 'brand',
}: {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  hint?: string;
  tone?: 'brand' | 'gold' | 'success' | 'danger' | 'muted';
}) {
  const toneClass: Record<string, string> = {
    brand: 'bg-brand-50 text-brand-600',
    gold: 'bg-gold-500/10 text-gold-700',
    success: 'bg-success/10 text-success',
    danger: 'bg-danger/10 text-danger',
    muted: 'bg-black/5 text-muted',
  };
  return (
    <div className="card p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</span>
        {icon && <span className={`grid h-8 w-8 place-items-center rounded-lg ${toneClass[tone]}`}>{icon}</span>}
      </div>
      <p className="mt-2 text-2xl font-extrabold text-ink">{value}</p>
      {hint && <p className="mt-0.5 text-xs text-muted">{hint}</p>}
    </div>
  );
}

/* ---------- Section card ---------- */
export function SectionCard({
  title,
  action,
  children,
  className = '',
}: {
  title?: string;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`card p-5 ${className}`}>
      {(title || action) && (
        <div className="mb-4 flex items-center justify-between gap-2">
          {title && <h2 className="font-bold text-ink">{title}</h2>}
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

/* ---------- List skeleton ---------- */
export function ListLoading({ rows = 3 }: { rows?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-16 w-full" />
      ))}
    </div>
  );
}

/**
 * Renders the right state for a react-query result: loading skeleton, error with retry,
 * an empty state when the resolved data is empty, otherwise the children.
 */
export function Async<T>({
  query,
  emptyTitle = 'Nothing here yet',
  emptySubtitle,
  emptyIcon,
  emptyAction,
  loading,
  isEmpty,
  children,
}: {
  query: UseQueryResult<T>;
  emptyTitle?: string;
  emptySubtitle?: string;
  emptyIcon?: ReactNode;
  emptyAction?: ReactNode;
  loading?: ReactNode;
  isEmpty?: (data: T) => boolean;
  children: (data: T) => ReactNode;
}) {
  if (query.isLoading) return <>{loading ?? <ListLoading />}</>;
  if (query.isError || query.data === undefined)
    return <ErrorState message="We couldn't load this. Check your connection and try again." onRetry={() => query.refetch()} />;
  const data = query.data;
  const empty = isEmpty ? isEmpty(data) : Array.isArray(data) && data.length === 0;
  if (empty) return <EmptyState icon={emptyIcon} title={emptyTitle} subtitle={emptySubtitle} action={emptyAction} />;
  return <>{children(data)}</>;
}

/* ---------- Simple row ---------- */
export function Row({
  icon,
  title,
  subtitle,
  meta,
  action,
}: {
  icon?: ReactNode;
  title: ReactNode;
  subtitle?: ReactNode;
  meta?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-line bg-white p-3.5">
      {icon && <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-brand-50 text-brand-600">{icon}</div>}
      <div className="min-w-0 flex-1">
        <div className="truncate font-semibold text-ink">{title}</div>
        {subtitle && <div className="truncate text-sm text-muted">{subtitle}</div>}
      </div>
      {meta && <div className="shrink-0 text-right">{meta}</div>}
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}
