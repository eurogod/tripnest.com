import type { ReactNode } from 'react';
import type { AsyncState } from '../hooks/useAsync';

interface AsyncBoundaryProps<T> {
  state: AsyncState<T>;
  children: (data: T) => ReactNode;
//   Return true to show the empty state instead of children.
  isEmpty?: (data: T) => boolean;
  loadingMessage?: string;
  errorMessage?: string;
  emptyMessage?: string;
}

// Renders consistent loading / error / empty / data states for useAsync.
export default function AsyncBoundary<T>({
  state,
  children,
  isEmpty,
  loadingMessage = 'Loading…',
  errorMessage = 'Something went wrong.',
  emptyMessage = 'Nothing here yet.',
}: AsyncBoundaryProps<T>) {
  if (state.loading) return <p className="text-muted">{loadingMessage}</p>;
  if (state.error) return <p className="text-rose-600">{errorMessage}</p>;
  if (state.data === undefined) return null;
  if (isEmpty?.(state.data)) return <p className="text-muted">{emptyMessage}</p>;
  return <>{children(state.data)}</>;
}
