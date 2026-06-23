import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AuthShell } from './AuthShell';
import { Button, Input } from '@/components/ui';
import { useAuth, roleHome } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';

const DEMO = [
  { label: 'Tenant', email: 'kofi@tripnest.local', password: 'Tenant@123456' },
  { label: 'Landlord', email: 'kwame@tripnest.local', password: 'Landlord@123456' },
  { label: 'Agent', email: 'ekow@tripnest.local', password: 'Agent@123456' },
  { label: 'Caretaker', email: 'ebo@tripnest.local', password: 'Caretaker@123456' },
  { label: 'Admin', email: 'admin@tripnest.local', password: 'Admin@123456' },
];

export default function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const toast = useToast();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [loading, setLoading] = useState(false);

  const from = (location.state as { from?: string } | null)?.from;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      const user = await login(email, password);
      toast.success(`Welcome back, ${user.fullName.split(' ')[0]}!`);
      navigate(from ?? roleHome(user.role), { replace: true });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Login failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell title="Welcome back" subtitle="Log in to your TripNest account.">
      <form onSubmit={submit} className="space-y-4">
        <Input
          label="Email"
          type="email"
          name="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@example.com"
        />
        <div>
          <Input
            label="Password"
            type={show ? 'text' : 'password'}
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="••••••••"
          />
          <div className="mt-1.5 flex items-center justify-between">
            <label className="flex items-center gap-1.5 text-xs text-muted">
              <input type="checkbox" checked={show} onChange={(e) => setShow(e.target.checked)} /> Show password
            </label>
            <Link to="/forgot-password" className="text-xs font-semibold text-brand-700 hover:underline">
              Forgot password?
            </Link>
          </div>
        </div>
        <Button type="submit" block loading={loading}>
          Log in
        </Button>
      </form>

      <p className="mt-5 text-center text-sm text-muted">
        New to TripNest?{' '}
        <Link to="/signup" className="font-bold text-brand-700 hover:underline">
          Create an account
        </Link>
      </p>

      <details className="mt-6 rounded-lg border border-line bg-white p-3 text-sm">
        <summary className="cursor-pointer font-semibold text-muted">Demo accounts (dev)</summary>
        <div className="mt-2 flex flex-wrap gap-2">
          {DEMO.map((d) => (
            <button
              key={d.label}
              type="button"
              onClick={() => {
                setEmail(d.email);
                setPassword(d.password);
              }}
              className="rounded-full border border-line px-3 py-1 text-xs font-semibold hover:border-brand-600 hover:text-brand-700"
            >
              {d.label}
            </button>
          ))}
        </div>
      </details>
    </AuthShell>
  );
}
