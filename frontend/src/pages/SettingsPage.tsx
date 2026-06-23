import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { authApi, prefsApi, profileApi, safetyApi } from '@/lib/services';
import { PageHeader, SectionCard } from '@/components/dashboard';
import { Button, Input } from '@/components/ui';
import { Logout } from '@/components/icons';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';
import { UserRoleLabel } from '@/lib/enums';

function Toggle({ checked, onChange, label, hint }: { checked: boolean; onChange: (v: boolean) => void; label: string; hint?: string }) {
  return (
    <label className="flex items-center justify-between gap-4 py-2">
      <span>
        <span className="block text-sm font-semibold text-ink">{label}</span>
        {hint && <span className="block text-xs text-muted">{hint}</span>}
      </span>
      <button
        type="button"
        onClick={() => onChange(!checked)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition ${checked ? 'bg-brand-600' : 'bg-line'}`}
        aria-pressed={checked}
      >
        <span className={`absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition ${checked ? 'left-[22px]' : 'left-0.5'}`} />
      </button>
    </label>
  );
}

export default function SettingsPage() {
  const { user, patchUser, logout, refreshUser } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [pw, setPw] = useState({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
  const [prefs, setPrefs] = useState({ smsEnabled: true, emailEnabled: true });
  const [contact, setContact] = useState({ name: '', phone: '', email: '' });
  const [saving, setSaving] = useState<string | null>(null);

  const prefsQuery = useQuery({ queryKey: ['prefs'], queryFn: prefsApi.get, enabled: !!user });
  const contactQuery = useQuery({ queryKey: ['trusted-contact'], queryFn: safetyApi.getContact, enabled: !!user });

  useEffect(() => {
    if (prefsQuery.data) setPrefs({ smsEnabled: prefsQuery.data.smsEnabled, emailEnabled: prefsQuery.data.emailEnabled });
  }, [prefsQuery.data]);
  useEffect(() => {
    if (contactQuery.data)
      setContact({ name: contactQuery.data.name ?? '', phone: contactQuery.data.phone ?? '', email: contactQuery.data.email ?? '' });
  }, [contactQuery.data]);

  async function run(key: string, fn: () => Promise<void>, ok: string) {
    setSaving(key);
    try {
      await fn();
      toast.success(ok);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Something went wrong');
    } finally {
      setSaving(null);
    }
  }

  if (!user) return null;

  return (
    <div className="container-tn max-w-2xl py-8">
      <PageHeader title="Settings" subtitle={`Signed in as ${user.email} · ${UserRoleLabel[user.role]}`} />

      <div className="space-y-5">
        <SectionCard title="Profile">
          <div className="space-y-4">
            <Input label="Full name" value={fullName} onChange={(e) => setFullName(e.target.value)} />
            <Button
              size="sm"
              loading={saving === 'profile'}
              onClick={() =>
                run(
                  'profile',
                  async () => {
                    await profileApi.update({ fullName });
                    patchUser({ fullName });
                  },
                  'Profile updated',
                )
              }
            >
              Save profile
            </Button>
          </div>
        </SectionCard>

        <SectionCard title="Password">
          <div className="space-y-4">
            <Input label="Current password" type="password" value={pw.currentPassword} onChange={(e) => setPw({ ...pw, currentPassword: e.target.value })} />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Input label="New password" type="password" value={pw.newPassword} onChange={(e) => setPw({ ...pw, newPassword: e.target.value })} />
              <Input label="Confirm new" type="password" value={pw.confirmNewPassword} onChange={(e) => setPw({ ...pw, confirmNewPassword: e.target.value })} />
            </div>
            <Button
              size="sm"
              loading={saving === 'pw'}
              disabled={!pw.currentPassword || pw.newPassword.length < 6 || pw.newPassword !== pw.confirmNewPassword}
              onClick={() =>
                run(
                  'pw',
                  async () => {
                    await authApi.changePassword(pw);
                    setPw({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
                  },
                  'Password changed',
                )
              }
            >
              Change password
            </Button>
          </div>
        </SectionCard>

        <SectionCard title="Notifications">
          <Toggle checked={prefs.emailEnabled} onChange={(v) => setPrefs({ ...prefs, emailEnabled: v })} label="Email" hint="Receipts, agreements and account alerts." />
          <Toggle checked={prefs.smsEnabled} onChange={(v) => setPrefs({ ...prefs, smsEnabled: v })} label="SMS" hint="Safety alerts and time-sensitive updates." />
          <Button
            className="mt-3"
            size="sm"
            loading={saving === 'prefs'}
            onClick={() => run('prefs', () => prefsApi.update(prefs.smsEnabled, prefs.emailEnabled).then(() => {}), 'Preferences saved')}
          >
            Save preferences
          </Button>
        </SectionCard>

        <SectionCard title="Trusted safety contact">
          <p className="mb-4 text-sm text-muted">Who we notify if you raise a safety alert during a stay.</p>
          <div className="space-y-4">
            <Input label="Name" value={contact.name} onChange={(e) => setContact({ ...contact, name: e.target.value })} />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Input label="Phone" value={contact.phone} onChange={(e) => setContact({ ...contact, phone: e.target.value })} />
              <Input label="Email" type="email" value={contact.email} onChange={(e) => setContact({ ...contact, email: e.target.value })} />
            </div>
            <Button size="sm" loading={saving === 'contact'} onClick={() => run('contact', () => safetyApi.setContact(contact).then(() => {}), 'Contact saved')}>
              Save contact
            </Button>
          </div>
        </SectionCard>

        <SectionCard title="Session">
          <Button
            variant="danger"
            onClick={async () => {
              await logout();
              await refreshUser();
              navigate('/');
            }}
          >
            <Logout className="h-4 w-4" /> Sign out
          </Button>
        </SectionCard>
      </div>
    </div>
  );
}
