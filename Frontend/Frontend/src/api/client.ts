// Real HTTP client for the TripNest.Core API.
//
// Every backend response is wrapped in an envelope:
//   { message: string, statusCode: number, data: T | null, success: boolean }
// We unwrap `.data` here, once, so callers get their payload directly and
// failures surface as a thrown ApiError carrying the backend's message.
//
// Auth: a JWT access token is attached to every request. On a 401 we attempt
// one refresh-token exchange and retry; if that fails we broadcast
// `tripnest:unauthorized` so the auth store can clear the session.

// Empty by default: requests stay same-origin so the Vite dev proxy forwards
// them to the backend (Core only allows configured CORS origins). Production
// builds set VITE_API_URL to the API origin instead.
const BASE_URL: string = import.meta.env.VITE_API_URL ?? '';

/** Origin of the API (for /uploads/... file links served outside /api). */
export const API_ORIGIN = BASE_URL.replace(/\/api\/?$/, '');

/** Simulated network latency for mock-backed services. */
export const MOCK_DELAY = 400;

/** Resolve a value after a short delay, mimicking an async request. */
export function mockResponse<T>(value: T): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), MOCK_DELAY));
}

interface Envelope<T> {
  message: string;
  statusCode: number;
  data: T | null;
  success: boolean;
}

export class ApiError extends Error {
  statusCode: number;

  constructor(message: string, statusCode: number) {
    super(message);
    this.name = 'ApiError';
    this.statusCode = statusCode;
  }
}

/** Paged list shape returned by ?page=&pageSize= endpoints. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// --- token storage -----------------------------------------------------------

const ACCESS_KEY = 'tripnest.accessToken';
const REFRESH_KEY = 'tripnest.refreshToken';

export interface Tokens {
  accessToken: string;
  refreshToken: string;
}

export function getAccessToken(): string | null {
  try { return localStorage.getItem(ACCESS_KEY); } catch { return null; }
}

/** Current token pair, or null when signed out. */
export function getTokens(): Tokens | null {
  const accessToken = getAccessToken();
  if (!accessToken) return null;
  let refreshToken = '';
  try { refreshToken = localStorage.getItem(REFRESH_KEY) ?? ''; } catch { /* session-only auth */ }
  return { accessToken, refreshToken };
}

export function setTokens(access: string | null, refresh: string | null): void {
  try {
    if (access) localStorage.setItem(ACCESS_KEY, access); else localStorage.removeItem(ACCESS_KEY);
    if (refresh) localStorage.setItem(REFRESH_KEY, refresh); else localStorage.removeItem(REFRESH_KEY);
  } catch { /* storage unavailable — session-only auth */ }
}

export function clearTokens(): void {
  setTokens(null, null);
}

// --- refresh flow ------------------------------------------------------------

let refreshing: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  // Collapse concurrent 401s into a single refresh round-trip.
  refreshing ??= (async () => {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (!refreshToken) return false;
    try {
      const res = await fetch(`${BASE_URL}/api/auth/refresh-token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      const envelope = (await res.json()) as Envelope<{ accessToken: string; refreshToken: string }>;
      if (!envelope.success || !envelope.data) return false;
      setTokens(envelope.data.accessToken, envelope.data.refreshToken);
      return true;
    } catch {
      return false;
    }
  })().finally(() => { refreshing = null; });
  return refreshing;
}

// --- core request ------------------------------------------------------------

async function request<T>(method: string, path: string, body?: unknown, retried = false): Promise<T> {
  const token = getAccessToken();
  const isForm = typeof FormData !== 'undefined' && body instanceof FormData;

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: {
      ...(body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: isForm ? (body as FormData) : body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401 && token && !retried) {
    if (await tryRefresh()) return request<T>(method, path, body, true);
    setTokens(null, null);
    window.dispatchEvent(new Event('tripnest:unauthorized'));
  }

  let envelope: Envelope<T>;
  try {
    envelope = (await res.json()) as Envelope<T>;
  } catch {
    throw new ApiError(`Request to ${path} failed (${res.status})`, res.status);
  }
  if (!envelope.success) throw new ApiError(envelope.message || 'Request failed', envelope.statusCode);
  return envelope.data as T;
}

/**
 * List endpoints migrated to server-side pagination return { items, totalCount, ... } instead of a
 * bare array — a drift that silently broke every page typed as T[] (".map is not a function" →
 * the page showed "failed"). apiGetList accepts EITHER shape, so callers keep working whether or
 * not an endpoint is paginated yet.
 */
export function asItems<T>(data: T[] | Paged<T> | null | undefined): T[] {
  if (!data) return [];
  return Array.isArray(data) ? data : (data.items ?? []);
}
export const apiGetList = <T>(path: string) => request<T[] | Paged<T>>('GET', path).then(asItems);

export const apiGet = <T>(path: string) => request<T>('GET', path);
export const apiPost = <T>(path: string, body?: unknown) => request<T>('POST', path, body);
export const apiPut = <T>(path: string, body?: unknown) => request<T>('PUT', path, body);
export const apiPatch = <T>(path: string, body?: unknown) => request<T>('PATCH', path, body);
export const apiDelete = <T>(path: string) => request<T>('DELETE', path);
/** Multipart upload (photos, videos) — browser sets the boundary header. */
export const apiUpload = <T>(path: string, form: FormData) => request<T>('POST', path, form);

/** Absolute URL for a backend-served asset path (e.g. /uploads/properties/x.jpg). */
export function assetUrl(path: string): string {
  return /^https?:\/\//.test(path) ? path : `${API_ORIGIN}${path}`;
}

/** Fetch an authenticated binary (PDF) endpoint and hand it to the browser as a download. */
export async function apiDownload(path: string, filename: string): Promise<void> {
  const token = getAccessToken();
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });
  if (!res.ok) throw new ApiError(`Download from ${path} failed`, res.status);
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
