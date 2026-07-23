import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { homeForRole } from '../lib/roleHome';
import { consumeXCallback, consumeXSignupRole, xRedirectUri } from '../lib/xOauth';
import { exchangeXCode, signInWithX, type Role } from '../store/authStore';

/**
 * Lands here from X after the user authorizes (/auth/x/callback). Exchanges the one-time code
 * server-side, then signs in. X usually shares no email (that needs elevated API access), so a
 * FIRST sign-in commonly comes back 400 "provide an email" — we keep the exchanged access token
 * in memory and retry with the email the user types, without restarting the whole OAuth dance.
 */
export default function XCallbackPage() {
  const [query] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [needEmail, setNeedEmail] = useState(false);
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(true);
  const tokenRef = useRef<string | null>(null);
  const roleRef = useRef<Role | undefined>(undefined);
  const ran = useRef(false);

  const finish = async (suppliedEmail?: string) => {
    setBusy(true);
    setError(null);
    try {
      const session = await signInWithX(tokenRef.current!, suppliedEmail, roleRef.current);
      navigate(homeForRole(session.role), { replace: true });
    } catch (e) {
      if (e instanceof ApiError && /email/i.test(e.message) && !suppliedEmail) {
        setNeedEmail(true); // X shared no email — ask once, retry with the same token
      } else {
        setError(e instanceof Error ? e.message : 'Could not complete the X sign-in.');
      }
      setBusy(false);
    }
  };

  useEffect(() => {
    if (ran.current) return; // StrictMode double-invoke guard: the code is single-use
    ran.current = true;

    roleRef.current = consumeXSignupRole() as Role | undefined;
    const callback = consumeXCallback(query);

    (async () => {
      await Promise.resolve(); // effects must not set state synchronously
      if (!callback) {
        setError(query.get('error') === 'access_denied'
          ? 'X sign-in was cancelled.'
          : 'Could not complete the X sign-in. Please try again.');
        setBusy(false);
        return;
      }
      try {
        tokenRef.current = await exchangeXCode(callback.code, callback.verifier, xRedirectUri());
        await finish();
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not complete the X sign-in.');
        setBusy(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm text-center space-y-4">
        <h1 className="text-lg font-semibold">Signing in with X</h1>

        {busy && <p className="text-sm text-muted">Finishing up…</p>}

        {needEmail && !busy && (
          <form
            className="space-y-3 text-left"
            onSubmit={(e) => { e.preventDefault(); void finish(email.trim()); }}
          >
            <p className="text-sm text-muted">
              Your X account didn’t share an email address. Enter one to finish creating your
              TripNest account — you’ll verify it afterwards.
            </p>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@example.com"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
            />
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button type="submit" className="w-full rounded-lg bg-gray-900 px-3 py-2 text-sm font-medium text-white">
              Continue
            </button>
          </form>
        )}

        {error && !needEmail && !busy && (
          <>
            <p className="text-sm text-red-600">{error}</p>
            <button
              onClick={() => navigate('/welcome', { replace: true })}
              className="rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white"
            >
              Back to sign in
            </button>
          </>
        )}
      </div>
    </div>
  );
}
