// Sign in with X (Twitter) — OAuth 2.0 authorization-code flow with PKCE.
//
// The browser only runs the redirect dance: build a verifier/challenge pair, send the user to
// x.com to authorize, and land back on /auth/x/callback with a one-time code. The code exchange
// happens on the BACKEND (POST /api/auth/x/exchange) because the X app is a confidential client —
// the client secret must never ship in this bundle. PKCE still protects the code in transit.

const X_CLIENT_ID = (import.meta.env as Record<string, string | undefined>).VITE_X_CLIENT_ID;

const VERIFIER_KEY = 'tripnest.x.verifier';
const STATE_KEY = 'tripnest.x.state';
const ROLE_KEY = 'tripnest.x.role';

/** True when sign-in with X is configured for this build. */
export const xSignInEnabled = (): boolean => Boolean(X_CLIENT_ID);

/** The registered callback URI — must byte-match one in the X developer portal. */
export const xRedirectUri = (): string => `${window.location.origin}/auth/x/callback`;

function randomUrlSafe(bytes: number): string {
  const raw = crypto.getRandomValues(new Uint8Array(bytes));
  return btoa(String.fromCharCode(...raw)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

async function sha256Challenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/**
 * Kicks off the flow: stores the PKCE verifier + anti-CSRF state (and the role picked on the
 * sign-up tab, which must survive the round-trip to x.com), then redirects to X.
 */
export async function startXSignIn(role?: string): Promise<void> {
  if (!X_CLIENT_ID) return;

  if (role) sessionStorage.setItem(ROLE_KEY, role);
  else sessionStorage.removeItem(ROLE_KEY);

  const verifier = randomUrlSafe(48);
  const state = randomUrlSafe(24);
  sessionStorage.setItem(VERIFIER_KEY, verifier);
  sessionStorage.setItem(STATE_KEY, state);

  const params = new URLSearchParams({
    response_type: 'code',
    client_id: X_CLIENT_ID,
    redirect_uri: xRedirectUri(),
    // users.read + tweet.read are both required by GET /2/users/me; users.email adds the
    // confirmed-email fast path when the app tier exposes it.
    scope: 'users.read tweet.read users.email',
    state,
    code_challenge: await sha256Challenge(verifier),
    code_challenge_method: 'S256',
  });

  window.location.assign(`https://x.com/i/oauth2/authorize?${params}`);
}

/**
 * Validates the callback query and hands back the code + verifier for the backend exchange.
 * Returns null when the state doesn't match (or the flow wasn't started here) — treat as failed.
 */
export function consumeXCallback(query: URLSearchParams): { code: string; verifier: string } | null {
  const code = query.get('code');
  const state = query.get('state');
  const verifier = sessionStorage.getItem(VERIFIER_KEY);
  const expectedState = sessionStorage.getItem(STATE_KEY);
  sessionStorage.removeItem(VERIFIER_KEY);
  sessionStorage.removeItem(STATE_KEY);

  if (!code || !verifier || !state || state !== expectedState) return null;
  return { code, verifier };
}

/** The role picked before the redirect (sign-up tab only); consumed once by the callback page. */
export function consumeXSignupRole(): string | undefined {
  const role = sessionStorage.getItem(ROLE_KEY) ?? undefined;
  sessionStorage.removeItem(ROLE_KEY);
  return role;
}
