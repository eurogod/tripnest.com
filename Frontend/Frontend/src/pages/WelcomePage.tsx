import { useState } from 'react';
import { homeForRole } from '../lib/roleHome';
import SocialSignIn from '../components/SocialSignIn';
import ForgotPassword from '../components/ForgotPassword';
import { Navigate, useNavigate } from 'react-router-dom';
import { signIn, register, useSession, type Role } from '../store/authStore';
import { HexIcon, UserIcon, KeyIcon, ShieldIcon, StarIcon, ToolIcon , UserCheckIcon} from '../components/tenant/icons';

// Poster-style background tiles (Ghanaian cities), echoing the inspiration wall.
const TILES: { city: string; from: string; to: string }[] = [
  { city: 'Tarkwa', from: '#0f5132', to: '#34d399' },
  { city: 'Accra', from: '#1e3a8a', to: '#60a5fa' },
  { city: 'Kumasi', from: '#9d174d', to: '#fb7185' },
  { city: 'Takoradi', from: '#7c2d12', to: '#fbbf24' },
  { city: 'Cape Coast', from: '#155e75', to: '#22d3ee' },
  { city: 'Tamale', from: '#4c1d95', to: '#a78bfa' },
  { city: 'Ho', from: '#065f46', to: '#6ee7b7' },
  { city: 'Sunyani', from: '#92400e', to: '#fcd34d' },
  { city: 'Koforidua', from: '#1e40af', to: '#93c5fd' },
  { city: 'Wa', from: '#831843', to: '#f9a8d4' },
  { city: 'Sekondi', from: '#0c4a6e', to: '#7dd3fc' },
  { city: 'Obuasi', from: '#3f6212', to: '#bef264' },
  { city: 'Tema', from: '#581c87', to: '#d8b4fe' },
  { city: 'Aburi', from: '#14532d', to: '#86efac' },
  { city: 'Elmina', from: '#9a3412', to: '#fdba74' },
  { city: 'Bolga', from: '#0f766e', to: '#5eead4' },
  { city: 'Axim', from: '#1d4ed8', to: '#bfdbfe' },
  { city: 'Nkawkaw', from: '#a16207', to: '#fde68a' },
];

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** A poster tile showing a real photo, with a gradient fallback + city label. */
function Tile({ city, from, to }: { city: string; from: string; to: string }) {
  return (
    <div
      className="relative overflow-hidden rounded-2xl shadow-lg ring-1 ring-black/5"
      style={{ backgroundImage: `linear-gradient(150deg, ${from}, ${to})` }}
    >
      <img
        src={`https://picsum.photos/seed/tripnest-${encodeURIComponent(city)}/440/560`}
        alt=""
        loading="lazy"
        className="absolute inset-0 h-full w-full object-cover"
        onError={(e) => { e.currentTarget.style.display = 'none'; }}
      />
      <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/60 to-transparent p-3 pt-8">
        <span className="text-sm font-bold uppercase tracking-wide text-white drop-shadow">{city}</span>
      </div>
    </div>
  );
}

function RoleOption({ active, icon, title, desc, onClick }: {
  active: boolean; icon: React.ReactNode; title: string; desc: string; onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`flex flex-1 items-start gap-2.5 rounded-xl border p-3 text-left transition-colors ${
        active ? 'border-brand bg-brand-50' : 'border-gray-200 hover:bg-gray-50'
      }`}
    >
      <span className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${active ? 'bg-brand text-white' : 'bg-gray-100 text-gray-500'}`}>
        {icon}
      </span>
      <span className="min-w-0">
        <span className={`block text-sm font-semibold ${active ? 'text-brand' : 'text-ink'}`}>{title}</span>
        <span className="block text-xs text-muted">{desc}</span>
      </span>
    </button>
  );
}

export default function WelcomePage() {
  const navigate = useNavigate();
  const session = useSession();
  const [mode, setMode] = useState<'signin' | 'signup' | 'forgot'>('signin');
  const [role, setRole] = useState<Role>('tenant');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Already signed in → straight to the right surface.
  if (session) return <Navigate to={homeForRole(session.role)} replace />;




  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const addr = email.trim().toLowerCase();
    if (!EMAIL_RE.test(addr)) {
      setError('Enter a valid email address.');
      return;
    }
    if (mode === 'signup' && password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }
    setBusy(true);
    try {
      const s = mode === 'signin'
        ? await signIn(email.trim().toLowerCase(), password)
        : await register({
            fullName: fullName.trim(),
            email: email.trim().toLowerCase(),
            password,
            confirmPassword,
            phone: phone.trim(),
            role,
          });
      // New accounts go through identity verification (with a skip); returning users go straight in.
      navigate(mode === 'signup' ? '/get-verified' : homeForRole(s.role), { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong. Please try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="relative min-h-screen overflow-hidden bg-ink">
      {/* Poster grid of real photos */}
      <div aria-hidden className="absolute inset-0 grid h-full auto-rows-fr grid-cols-3 gap-3 p-3 sm:grid-cols-4 lg:grid-cols-6">
        {TILES.map((t) => <Tile key={t.city} {...t} />)}
      </div>
      {/* Legibility wash + brand glow */}
      <div aria-hidden className="absolute inset-0 bg-gradient-to-br from-ink/65 via-ink/40 to-brand/45" />
      <div aria-hidden className="absolute inset-0 backdrop-blur-[2px]" />

      {/* Brand mark */}
      <div className="relative flex items-center gap-2 p-5">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-white text-brand shadow-lg">
          <HexIcon size={20} />
        </span>
        <span className="text-lg font-bold text-white drop-shadow">TripNest</span>
      </div>

      {/* Card */}
      <div className="relative flex min-h-[calc(100vh-5rem)] items-center justify-center px-4 pb-10">
        <div className="tn-card-in w-full max-w-md overflow-hidden rounded-[28px] bg-white/95 shadow-[0_30px_80px_-20px_rgba(0,0,0,0.55)] ring-1 ring-white/60 backdrop-blur-xl">
          <div className="h-1.5 bg-gradient-to-r from-brand via-emerald-400 to-brand" />
          <div className="p-6 sm:p-8">
          <div className="mb-5 text-center">
            <span className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-brand to-emerald-500 text-white shadow-lg">
              <HexIcon size={28} />
            </span>
            <h1 className="text-2xl font-bold text-ink">Welcome to TripNest</h1>
            <p className="mt-1 text-sm text-muted">Find · Stay · Thrive — verified homes across Ghana.</p>
            <div className="mt-3 flex items-center justify-center gap-3 text-xs text-muted">
              <span className="flex items-center gap-1"><ShieldIcon size={13} className="text-brand" /> Verified listings</span>
              <span className="flex items-center gap-1">
                <StarIcon size={13} className="text-amber-400" /> Escrow-protected stays
              </span>
            </div>
          </div>

          {/* Role choice (sign-up only — sign-in derives the role from the account) */}
          {mode === 'signup' && (
            <>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">I want to join as</p>
              <div className="mb-5 grid grid-cols-2 gap-3">
                <RoleOption
                  active={role === 'tenant'}
                  icon={<UserIcon size={16} />}
                  title="Tenant"
                  desc="Find & book places"
                  onClick={() => setRole('tenant')}
                />
                <RoleOption
                  active={role === 'landlord'}
                  icon={<KeyIcon size={16} />}
                  title="Landlord"
                  desc="Host & earn"
                  onClick={() => setRole('landlord')}
                />
                <RoleOption
                  active={role === 'agent'}
                  icon={<UserCheckIcon size={16} />}
                  title="Agent"
                  desc="List & do viewings"
                  onClick={() => setRole('agent')}
                />
                <RoleOption
                  active={role === 'caretaker'}
                  icon={<ToolIcon size={16} />}
                  title="Caretaker"
                  desc="Manage & maintain"
                  onClick={() => setRole('caretaker')}
                />
              </div>
              {(role === 'landlord' || role === 'agent' || role === 'caretaker') && (
                <p className="-mt-3 mb-4 text-xs text-muted">
                  You'll complete Ghana Card verification from your profile before you can list or work.
                </p>
              )}
            </>
          )}

          {mode === 'forgot' ? (
            <ForgotPassword onDone={() => { setMode('signin'); setError(null); }} />
          ) : (
          <>
          <form onSubmit={submit} className="space-y-3">
            {mode === 'signup' && (
              <input
                value={fullName}
                onChange={(e) => { setFullName(e.target.value); setError(null); }}
                placeholder="Full name"
                aria-label="Full name"
                required
                className="w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand"
              />
            )}
            <input
              type="email"
              value={email}
              onChange={(e) => { setEmail(e.target.value); setError(null); }}
              placeholder="Email address"
              aria-label="Email address"
              required
              className="w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand"
            />
            {mode === 'signup' && (
              <input
                value={phone}
                onChange={(e) => { setPhone(e.target.value); setError(null); }}
                inputMode="tel"
                placeholder="Phone (e.g. 024 123 4567)"
                aria-label="Phone number"
                required
                className="w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand"
              />
            )}
            <input
              type="password"
              value={password}
              onChange={(e) => { setPassword(e.target.value); setError(null); }}
              placeholder="Password"
              aria-label="Password"
              required
              className="w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand"
            />
            {mode === 'signin' && (
              <button
                type="button"
                onClick={() => { setMode('forgot'); setError(null); }}
                className="block w-full text-right text-xs font-semibold text-brand"
              >
                Forgot password?
              </button>
            )}
            {mode === 'signup' && (
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => { setConfirmPassword(e.target.value); setError(null); }}
                placeholder="Confirm password"
                aria-label="Confirm password"
                required
                className="w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand"
              />
            )}
            {error && <p className="text-xs text-rose-600" role="alert">{error}</p>}
            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-xl bg-brand py-3 text-sm font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-60"
            >
              {busy ? 'Please wait…' : mode === 'signin' ? 'Sign in' : 'Create account'}
            </button>
          </form>

          <div className="mt-4">
            <SocialSignIn signupRole={mode === 'signup' ? role : undefined} onSignedIn={(s) => navigate(homeForRole(s.role), { replace: true })} />
          </div>
          </>
          )}

          <p className="mt-4 text-center text-sm text-muted">
            {mode === 'signin' ? (
              <>New to TripNest?{' '}
                <button type="button" onClick={() => { setMode('signup'); setError(null); }} className="font-semibold text-brand">
                  Create an account
                </button>
              </>
            ) : (
              <>Already have an account?{' '}
                <button type="button" onClick={() => { setMode('signin'); setError(null); }} className="font-semibold text-brand">
                  Sign in
                </button>
              </>
            )}
          </p>

          <p className="mt-5 text-center text-[11px] leading-relaxed text-muted">
            By continuing you agree to TripNest's{' '}
            <a href="#" className="font-semibold text-brand no-underline">Terms</a> and{' '}
            <a href="#" className="font-semibold text-brand no-underline">Privacy Policy</a>.
          </p>
          </div>
        </div>
      </div>
    </div>
  );
}
