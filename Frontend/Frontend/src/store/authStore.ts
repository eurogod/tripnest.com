import { useSyncExternalStore } from 'react';
import { apiGet, apiPost, setTokens, getAccessToken } from '../api/client';
import { roleFromInt, roleToInt } from '../lib/enums';

export type Role = 'tenant' | 'landlord' | 'agent' | 'caretaker' | 'admin' | 'guest';

export interface Session {
  userId: string;
  email: string;
  name: string;
  role: Role;
  /** Ghana Card identity verification (gates landlord/agent/caretaker actions). */
  verified: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  tripNestId?: string | null;
}

// Wire shape of POST /auth/login and /auth/refresh-token responses.
interface LoginData {
  userId: string;
  fullName: string;
  email: string;
  role: number;
  accessToken: string;
  refreshToken: string;
  isVerified: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  tripNestId?: string | null;
}

// ---------------------------------------------------------------------------
// Session state: cached in localStorage for instant paint, but the JWT in
// api/client.ts is what actually authenticates requests. When the client
// exhausts its refresh attempt it emits `tripnest:unauthorized` and we clear.
// ---------------------------------------------------------------------------

const KEY = 'tripnest.session';
const listeners = new Set<() => void>();

function readInitial(): Session | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;
    // A cached session without a token can't make requests — treat as signed out.
    if (!getAccessToken()) { localStorage.removeItem(KEY); return null; }
    return JSON.parse(raw) as Session;
  } catch {
    return null;
  }
}

let session: Session | null = readInitial();

function emit() {
  listeners.forEach((l) => l());
}

function setSession(next: Session | null): void {
  session = next;
  try {
    if (next) localStorage.setItem(KEY, JSON.stringify(next));
    else localStorage.removeItem(KEY);
  } catch { /* ignore */ }
  emit();
}

function toSession(d: LoginData): Session {
  return {
    userId: d.userId,
    email: d.email,
    name: d.fullName,
    role: roleFromInt(d.role),
    verified: d.isVerified,
    emailVerified: d.emailVerified,
    phoneVerified: d.phoneVerified,
    tripNestId: d.tripNestId,
  };
}

export function getSession(): Session | null {
  return session;
}

/** Sign in against the real API. Throws ApiError with the backend message on failure. */
export async function signIn(email: string, password: string): Promise<Session> {
  const data = await apiPost<LoginData>('/api/auth/login', { email, password });
  setTokens(data.accessToken, data.refreshToken);
  const next = toSession(data);
  setSession(next);
  return next;
}

/** Request a password-reset code to be emailed. Always resolves (server doesn't reveal if the email exists). */
export const requestPasswordReset = (email: string) =>
  apiPost('/api/auth/forgot-password', { email });

/** Complete a password reset with the emailed code. */
export const resetPassword = (email: string, resetToken: string, newPassword: string) =>
  apiPost('/api/auth/reset-password', { email, resetToken, newPassword, confirmPassword: newPassword });

/** Sign in with a Google ID token (from Google Identity Services). Provisions on first use;
 * `role` applies only to that first sign-up (the backend never changes an existing role). */
export async function signInWithGoogle(idToken: string, role?: Role): Promise<Session> {
  const data = await apiPost<LoginData>('/api/auth/google', { idToken, role: role ? roleToInt(role) : undefined });
  setTokens(data.accessToken, data.refreshToken);
  const next = toSession(data);
  setSession(next);
  return next;
}

/** Server-side X code exchange (confidential client — the secret lives on the backend). */
export async function exchangeXCode(code: string, codeVerifier: string, redirectUri: string): Promise<string> {
  const data = await apiPost<{ accessToken: string }>('/api/auth/x/exchange', { code, codeVerifier, redirectUri });
  return data.accessToken;
}

/**
 * Sign in with an X access token. Keyed on the X account id server-side; `email` is only needed
 * on FIRST sign-in when X shares no confirmed email (the backend's 400 says so) — retry with it.
 */
export async function signInWithX(accessToken: string, email?: string, role?: Role): Promise<Session> {
  const data = await apiPost<LoginData>('/api/auth/x', { accessToken, email, role: role ? roleToInt(role) : undefined });
  setTokens(data.accessToken, data.refreshToken);
  const next = toSession(data);
  setSession(next);
  return next;
}

export interface RegisterInput {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  phone: string;
  role: Role;
}

/** Register, then sign straight in with the same credentials. */
export async function register(input: RegisterInput): Promise<Session> {
  await apiPost('/api/auth/register', { ...input, role: roleToInt(input.role) });
  return signIn(input.email, input.password);
}

export function signOut(): void {
  // Best-effort server-side refresh-token revocation; local state clears regardless.
  apiPost('/api/auth/logout').catch(() => { /* already signed out server-side */ });
  // Close the chat socket so the next sign-in gets a fresh authenticated one.
  // Dynamic import keeps the SignalR client out of the auth bundle path.
  void import('../lib/chatHub').then((m) => m.disconnectChat()).catch(() => {});
  setTokens(null, null);
  setSession(null);
}

/** Patch local session fields after e.g. a profile update. */
export function updateSession(patch: Partial<Session>): void {
  if (session) setSession({ ...session, ...patch });
}

/** Re-pull verification flags & profile (e.g. after OTP or Ghana Card checks). */
export async function refreshSessionFromServer(): Promise<void> {
  if (!session) return;
  try {
    // /profile/me carries the live verification flags (from the DB); /auth/me only echoes JWT
    // claims and would report everyone as unverified, wiping a just-verified user's status.
    const me = await apiGet<{
      fullName: string; email: string; isVerified: boolean;
      emailVerified: boolean; phoneVerified: boolean; tripNestId?: string | null;
    }>('/api/profile/me');
    updateSession({
      name: me.fullName,
      email: me.email,
      verified: me.isVerified,
      emailVerified: me.emailVerified,
      phoneVerified: me.phoneVerified,
      tripNestId: me.tripNestId,
    });
  } catch { /* token invalid → the client's 401 path will clear us */ }
}

// Clear the session when the API client gives up on re-authentication.
if (typeof window !== 'undefined') {
  window.addEventListener('tripnest:unauthorized', () => setSession(null));
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

/** Subscribe a component to the current session. */
export function useSession(): Session | null {
  return useSyncExternalStore(subscribe, getSession, () => null);
}

/** Derive a display name from an email local part, e.g. "kofi.mensah" → "Kofi Mensah". */
export function nameFromEmail(email: string): string {
  const local = email.split('@')[0]?.replace(/[._-]+/g, ' ') ?? '';
  const name = local
    .split(' ')
    .filter(Boolean)
    .map((w) => w[0].toUpperCase() + w.slice(1))
    .join(' ');
  return name || 'Guest';
}



// import { useSyncExternalStore } from 'react';
// import { apiPost, clearTokens, setTokens , getAccessToken} from '../api/client';
// import type { LoginResponseDto , } from '../api/backend';

// export type Role = 'tenant' | 'landlord' | 'agent' | 'caretaker' | 'admin' | 'guest';
// export type AuthProvider = 'email' | 'google' | 'apple';

// export interface Session {
//   userId: string;
//   email: string;
//   name: string;
//   role: Role;
//   provider: AuthProvider;
//   isVerified: boolean;
//   emailVerified: boolean;
//   phoneVerified: boolean;
//   tripNestId?: string;
// }

// // Wire shape of POST /auth/login and /auth/refresh-token responses.
// interface LoginData {
//   userId: string;
//   fullName: string;
//   email: string;
//   role: number;
//   accessToken: string;
//   refreshToken: string;
//   isVerified: boolean;
//   emailVerified: boolean;
//   phoneVerified: boolean;
//   tripNestId?: string | null;
// }

// // ---------------------------------------------------------------------------
// // Session state: cached in localStorage for instant paint, but the JWT in
// // api/client.ts is what actually authenticates requests. When the client
// // exhausts its refresh attempt it emits `tripnest:unauthorized` and we clear.
// // ---------------------------------------------------------------------------

// const KEY = 'tripnest.session';
// const listeners = new Set<() => void>();

// function readInitial(): Session | null {
//   try {
//     const raw = localStorage.getItem(KEY);
//     if (!raw) return null;
//     // A cached session without a token can't make requests — treat as signed out.
//     if (!getAccessToken()) { localStorage.removeItem(KEY); return null; }
//     return JSON.parse(raw) as Session;
//   } catch {
//     return null;
//   }
// }

// // ---------------------------------------------------------------------------
// // Session backed by TripNest.Core JWT auth. The access/refresh tokens live in
// // api/client.ts token storage; this store keeps the user profile and notifies
// // React via useSyncExternalStore. Consumers (useSession, RequireAuth) are
// // unchanged from the mock era.
// // ---------------------------------------------------------------------------

// // const KEY = 'tripnest.session';
// // const listeners = new Set<() => void>();

// // function readInitial(): Session | null {
// //   try {
// //     const raw = localStorage.getItem(KEY);
// //     const parsed = raw ? (JSON.parse(raw) as Session) : null;
// //     // Discard pre-API sessions that carry no userId — they have no tokens.
// //     return parsed && parsed.userId ? parsed : null;
// //   } catch {
// //     return null;
// //   }
// // }

// let session: Session | null = readInitial();

// function emit() {
//   listeners.forEach((l) => l());
// }

// export function getSession(): Session | null {
//   return session;
// }

// function persist(next: Session | null): void {
//   session = next;
//   try {
//     if (next) localStorage.setItem(KEY, JSON.stringify(next));
//     else localStorage.removeItem(KEY);
//   } catch { /* ignore */ }
//   emit();
// }

// /** UserRole wire values: 0 Tenant, 1 Landlord, 2 Agent, 3 Caretaker, 4 Admin, 5 Guest. */
// function roleFromApi(role: number): Role {
//   return role === 1 ? 'landlord' : 'tenant';
// }

// function roleToApi(role: Role): number {
//   return role === 'landlord' ? 1 : 0;
// }

// function sessionFromLogin(dto: LoginResponseDto): Session {
//   return {
//     userId: dto.userId,
//     email: dto.email,
//     name: dto.fullName,
//     role: roleFromApi(dto.role),
//     provider: 'email',
//     isVerified: dto.isVerified,
//     emailVerified: dto.emailVerified,
//     phoneVerified: dto.phoneVerified,
//     tripNestId: dto.tripNestId ?? undefined,
//   };
// }

// /** POST /api/auth/login — stores tokens and opens a session. */
// export async function login(email: string, password: string): Promise<Session> {
//   const dto = await apiPost<LoginResponseDto>('/api/auth/login', { email, password });
//   setTokens({ accessToken: dto.accessToken, refreshToken: dto.refreshToken });
//   const next = sessionFromLogin(dto);
//   persist(next);
//   return next;
// }

// export interface RegisterInput {
//   fullName: string;
//   email: string;
//   password: string;
//   phone: string;
//   role: Role;
// }

// /** POST /api/auth/register, then log straight in. */
// export async function register(input: RegisterInput): Promise<Session> {
//   await apiPost<unknown>('/api/auth/register', {
//     fullName: input.fullName,
//     email: input.email,
//     password: input.password,
//     confirmPassword: input.password,
//     phone: input.phone,
//     role: roleToApi(input.role),
//   });
//   return login(input.email, input.password);
// }

// /** Patch the local session copy (e.g. after a profile edit). */
// export function updateSession(partial: Partial<Session>): void {
//   if (session) persist({ ...session, ...partial });
// }

// export function signOut(): void {
//   clearTokens();
//   persist(null);
// }

// function subscribe(listener: () => void): () => void {
//   listeners.add(listener);
//   return () => { listeners.delete(listener); };
// }

// /** Subscribe a component to the current session. */
// export function useSession(): Session | null {
//   return useSyncExternalStore(subscribe, getSession, () => null);
// }
