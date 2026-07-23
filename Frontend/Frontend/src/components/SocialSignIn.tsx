import { useEffect, useRef } from 'react';
import { signInWithGoogle, type Role, type Session } from '../store/authStore';
import { startXSignIn, xSignInEnabled } from '../lib/xOauth';

const GOOGLE_CLIENT_ID = (import.meta.env as Record<string, string | undefined>).VITE_GOOGLE_CLIENT_ID;

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (opts: { client_id: string; callback: (r: { credential: string }) => void }) => void;
          renderButton: (el: HTMLElement, opts: Record<string, unknown>) => void;
        };
      };
    };
  }
}

/**
 * Social sign-in options. Google lights up the moment VITE_GOOGLE_CLIENT_ID (and the matching
 * backend GoogleAuth:ClientId) are set. X lights up with VITE_X_CLIENT_ID — it redirects through
 * X's OAuth page and returns via /auth/x/callback. Apple stays deferred until the iOS app.
 */
export default function SocialSignIn({ onSignedIn, signupRole }: { onSignedIn: (s: Session) => void; signupRole?: Role }) {
  const ref = useRef<HTMLDivElement>(null);
  // The GIS callback closure outlives renders — a ref keeps the CURRENT tab's role choice visible to it.
  const signupRoleRef = useRef<Role | undefined>(signupRole);
  useEffect(() => {
    signupRoleRef.current = signupRole;
  }, [signupRole]);

  useEffect(() => {
    if (!GOOGLE_CLIENT_ID) return;

    const init = () => {
      const g = window.google;
      if (!g || !ref.current) return;
      g.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: async (resp) => {
          try {
            const session = await signInWithGoogle(resp.credential, signupRoleRef.current);
            onSignedIn(session);
          } catch { /* surfaced by the caller's error handling if needed */ }
        },
      });
      g.accounts.id.renderButton(ref.current, { theme: 'outline', size: 'large', width: 320, text: 'continue_with' });
    };

    if (window.google) { init(); return; }
    let script = document.getElementById('google-gsi') as HTMLScriptElement | null;
    if (!script) {
      script = document.createElement('script');
      script.id = 'google-gsi';
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      document.head.appendChild(script);
    }
    script.addEventListener('load', init);
    return () => script?.removeEventListener('load', init);
  }, [onSignedIn]);

  // Nothing to show until a provider is configured — keep the auth screen clean.
  if (!GOOGLE_CLIENT_ID && !xSignInEnabled()) return null;

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-3 py-1">
        <span className="h-px flex-1 bg-gray-200" />
        <span className="text-xs text-muted">or continue with</span>
        <span className="h-px flex-1 bg-gray-200" />
      </div>
      <div ref={ref} className="flex justify-center" />
      {xSignInEnabled() && (
        <button
          type="button"
          onClick={() => { void startXSignIn(signupRoleRef.current); }}
          className="mx-auto flex w-80 max-w-full items-center justify-center gap-2 rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium hover:bg-gray-50"
        >
          {/* X logomark */}
          <svg viewBox="0 0 24 24" className="h-4 w-4" fill="currentColor" aria-hidden="true">
            <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z" />
          </svg>
          Continue with X
        </button>
      )}
    </div>
  );
}
