import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AuthShell } from './AuthShell';
import { Button, Input } from '@/components/ui';
import { authApi } from '@/lib/services';
import { useToast } from '@/components/Toast';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);
  const toast = useToast();
  const navigate = useNavigate();

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await authApi.forgotPassword(email);
      setSent(true);
    } catch {
      // The endpoint intentionally doesn't reveal whether an email exists.
      setSent(true);
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell title="Reset your password" subtitle="We'll email you a reset code.">
      {sent ? (
        <div className="space-y-4">
          <div className="rounded-xl border border-brand-600/20 bg-brand-50 p-4 text-sm text-brand-800">
            If an account exists for <strong>{email}</strong>, a reset code is on its way. Enter it on the next screen.
          </div>
          <Button block onClick={() => navigate(`/reset-password?email=${encodeURIComponent(email)}`)}>
            I have a code
          </Button>
          <button
            type="button"
            onClick={() => {
              setSent(false);
              toast.info('Try a different email');
            }}
            className="w-full text-center text-sm font-semibold text-muted hover:text-ink"
          >
            Use a different email
          </button>
        </div>
      ) : (
        <form onSubmit={submit} className="space-y-4">
          <Input label="Email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" />
          <Button type="submit" block loading={loading}>Send reset code</Button>
          <p className="text-center text-sm text-muted">
            Remembered it?{' '}
            <Link to="/login" className="font-bold text-brand-700 hover:underline">Back to login</Link>
          </p>
        </form>
      )}
    </AuthShell>
  );
}
