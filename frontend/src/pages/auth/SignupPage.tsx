import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { AuthShell } from './AuthShell';
import { Button, Input } from '@/components/ui';
import { useAuth, roleHome } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';
import { UserRole } from '@/lib/enums';
import { Home, Users, Wrench, MapPin } from '@/components/icons';

const ROLES = [
  { role: UserRole.Tenant, label: 'Tenant', desc: 'Find & book a verified home', icon: <Home className="h-5 w-5" /> },
  { role: UserRole.Landlord, label: 'Landlord', desc: 'List property & earn securely', icon: <MapPin className="h-5 w-5" /> },
  { role: UserRole.Agent, label: 'Agent', desc: 'Handle viewings & listings', icon: <Users className="h-5 w-5" /> },
  { role: UserRole.Caretaker, label: 'Caretaker', desc: 'Provide on-site services', icon: <Wrench className="h-5 w-5" /> },
];

const roleFromParam = (v: string | null): UserRole => {
  switch (v?.toLowerCase()) {
    case 'landlord': return UserRole.Landlord;
    case 'agent': return UserRole.Agent;
    case 'caretaker': return UserRole.Caretaker;
    default: return UserRole.Tenant;
  }
};

export default function SignupPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { register } = useAuth();
  const toast = useToast();

  const [step, setStep] = useState<1 | 2>(1);
  const [role, setRole] = useState<UserRole>(roleFromParam(params.get('role')));
  const [form, setForm] = useState({ fullName: '', email: '', phone: '', password: '', confirmPassword: '' });
  const [loading, setLoading] = useState(false);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (form.password !== form.confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }
    setLoading(true);
    try {
      const user = await register({ ...form, role });
      toast.success('Account created — welcome to TripNest!');
      navigate(roleHome(user.role), { replace: true });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Sign up failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell
      title={step === 1 ? 'Join TripNest' : 'Create your account'}
      subtitle={step === 1 ? 'How will you use TripNest?' : `Signing up as ${ROLES.find((r) => r.role === role)?.label}`}
    >
      {step === 1 ? (
        <div className="space-y-3">
          {ROLES.map((r) => (
            <button
              key={r.role}
              onClick={() => setRole(r.role)}
              className={`flex w-full items-center gap-3 rounded-xl border p-4 text-left transition ${
                role === r.role ? 'border-brand-600 bg-brand-50 ring-2 ring-brand-600/15' : 'border-line hover:border-ink'
              }`}
            >
              <span className={`grid h-10 w-10 place-items-center rounded-lg ${role === r.role ? 'bg-brand-600 text-white' : 'bg-surface text-brand-600'}`}>
                {r.icon}
              </span>
              <span>
                <span className="block font-bold">{r.label}</span>
                <span className="block text-sm text-muted">{r.desc}</span>
              </span>
            </button>
          ))}
          <Button block onClick={() => setStep(2)}>Continue</Button>
          <p className="text-center text-sm text-muted">
            Already have an account?{' '}
            <Link to="/login" className="font-bold text-brand-700 hover:underline">Log in</Link>
          </p>
        </div>
      ) : (
        <form onSubmit={submit} className="space-y-4">
          <Input label="Full name" name="fullName" required value={form.fullName} onChange={set('fullName')} placeholder="Ama Mensah" />
          <Input label="Email" type="email" name="email" required value={form.email} onChange={set('email')} placeholder="you@example.com" />
          <Input label="Phone" name="phone" required value={form.phone} onChange={set('phone')} placeholder="+233 24 123 4567" hint="Ghana number, used for OTP & safety alerts" />
          <Input label="Password" type="password" name="password" required value={form.password} onChange={set('password')} placeholder="At least 8 characters" />
          <Input label="Confirm password" type="password" name="confirmPassword" required value={form.confirmPassword} onChange={set('confirmPassword')} />
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => setStep(1)}>Back</Button>
            <Button type="submit" block loading={loading}>Create account</Button>
          </div>
          <p className="text-center text-xs text-muted">
            By signing up you agree to TripNest's Terms & Privacy Policy. Hosts, agents and caretakers must complete
            Ghana Card verification before listing or accepting work.
          </p>
        </form>
      )}
    </AuthShell>
  );
}
