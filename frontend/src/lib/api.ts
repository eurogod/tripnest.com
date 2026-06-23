import axios, { AxiosError, type AxiosRequestConfig } from 'axios';
import type { ApiResponse, LoginResponse } from '@/types/api';

const ACCESS_KEY = 'tn_access';
const REFRESH_KEY = 'tn_refresh';

export const tokenStore = {
  get access() {
    return localStorage.getItem(ACCESS_KEY);
  },
  get refresh() {
    return localStorage.getItem(REFRESH_KEY);
  },
  set(access: string, refresh: string) {
    localStorage.setItem(ACCESS_KEY, access);
    localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};

// Proxied to http://localhost:5091 by the Vite dev server (same-origin in the browser).
export const http = axios.create({ baseURL: '/api' });

http.interceptors.request.use((config) => {
  const token = tokenStore.access;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshing: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = tokenStore.refresh;
  if (!refreshToken) return null;
  try {
    // Bare axios so we don't loop through this interceptor.
    const { data } = await axios.post<ApiResponse<LoginResponse>>('/api/auth/refresh-token', {
      refreshToken,
    });
    if (data?.data?.accessToken) {
      tokenStore.set(data.data.accessToken, data.data.refreshToken);
      return data.data.accessToken;
    }
  } catch {
    /* fall through */
  }
  tokenStore.clear();
  return null;
}

http.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;
    const status = error.response?.status;

    if (status === 401 && original && !original._retried && tokenStore.refresh) {
      original._retried = true;
      refreshing ??= refreshAccessToken().finally(() => {
        refreshing = null;
      });
      const newToken = await refreshing;
      if (newToken) {
        original.headers = { ...original.headers, Authorization: `Bearer ${newToken}` };
        return http(original);
      }
      // Refresh failed → bounce to login.
      window.dispatchEvent(new CustomEvent('tn:unauthorized'));
    }
    return Promise.reject(error);
  },
);

/** Thrown for any non-2xx ApiResponse so callers get a clean message + status. */
export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
    this.name = 'ApiError';
  }
}

function toApiError(err: unknown): ApiError {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as ApiResponse<unknown> | undefined;
    return new ApiError(data?.message || err.message || 'Request failed', err.response?.status ?? 0);
  }
  return new ApiError('Unexpected error', 0);
}

/** Unwrap the ApiResponse envelope, returning `data` or throwing an ApiError. */
async function unwrap<T>(p: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  try {
    const { data } = await p;
    if (!data.success) throw new ApiError(data.message, data.statusCode);
    return data.data as T;
  } catch (err) {
    if (err instanceof ApiError) throw err;
    throw toApiError(err);
  }
}

export const api = {
  get: <T>(url: string, config?: AxiosRequestConfig) => unwrap<T>(http.get<ApiResponse<T>>(url, config)),
  post: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.post<ApiResponse<T>>(url, body, config)),
  put: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.put<ApiResponse<T>>(url, body, config)),
  patch: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.patch<ApiResponse<T>>(url, body, config)),
  delete: <T>(url: string, config?: AxiosRequestConfig) =>
    unwrap<T>(http.delete<ApiResponse<T>>(url, config)),
  /** Raw blob fetch (PDF downloads) with auth header attached. */
  blob: (url: string) => http.get(url, { responseType: 'blob' }),
};
