import { useEffect, useState } from 'react';

export interface AsyncState<T> {
  data: T | undefined;
  loading: boolean;
  error: Error | undefined;
}

/**
 * Runs an async function and tracks loading/error/data state. Re-runs when
 * `deps` change; ignores results from stale calls to avoid race conditions.
 */
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []): AsyncState<T> {
  const [state, setState] = useState<AsyncState<T>>({
    data: undefined,
    loading: true,
    error: undefined,
  });

  useEffect(() => {
    let active = true;

    async function load() {
      setState({ data: undefined, loading: true, error: undefined });
      try {
        const data = await fn();
        if (active) setState({ data, loading: false, error: undefined });
      } catch (error: unknown) {
        if (active) {
          setState({
            data: undefined,
            loading: false,
            error: error instanceof Error ? error : new Error(String(error)),
          });
        }
      }
    }

    void load();
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return state;
}
