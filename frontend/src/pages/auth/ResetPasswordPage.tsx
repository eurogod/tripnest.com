import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { AuthShell } from './AuthShell';
import { Button, Input } from '@/components/ui';
import { authApi } from '@/lib/services';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const [form, setForm] = useState({
    email: params.get('email') ?? '',
    resetToken: params.get('token') ?? '',
    newPassword: '',
    confirmPassword: '',
  });
  const [loading, setLoading] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (form.newPassword !== form.confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }
    setLoading(true);
    try {
      await authApi.resetPassword(form);
      toast.success('Password reset — please log in.');
      navigate('/login', { replace: true });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Reset failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthShell title="Set a new password" subtitle="Enter the code we emailed you.">
      <form onSubmit={submit} className="space-y-4">
        <Input label="Email" type="email" required value={form.email} onChange={set('email')} />
        <Input label="Reset code" required value={form.resetToken} onChange={set('resetToken')} placeholder="Code from email" />
        <Input label="New password" type="password" required value={form.newPassword} onChange={set('newPassword')} />
        <Input label="Confirm new password" type="password" required value={form.confirmPassword} onChange={set('confirmPassword')} />
        <Button type="submit" block loading={loading}>Reset password</Button>
        <p className="text-center text-sm text-muted">
          <Link to="/login" className="font-bold text-brand-700 hover:underline">Back to login</Link>
        </p>
      </form>
    </AuthShell>
  );
}
