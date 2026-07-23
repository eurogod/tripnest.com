import { useState } from 'react';
import {
  getMyAgentProfile, upsertMyAgentProfile,
  AGENT_STATUS_LABELS, type AgentProfileDto,
} from '../../api/agentWorkspace';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';

// The public directory profile — without it the agent never appears in the
// tenant-facing agents directory, so an empty state nudges them to create one.
export default function AgentProfilePage() {
  const state = useAsync(getMyAgentProfile);

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">My profile</h1>
      <p className="mt-1 mb-6 text-sm text-muted">
        Your public listing in the TripNest agents directory.
      </p>
      <AsyncBoundary state={state} errorMessage="Failed to load your profile.">
        {(profile) => <ProfileForm initial={profile} />}
      </AsyncBoundary>
    </div>
  );
}

function ProfileForm({ initial }: { initial: AgentProfileDto | null }) {
  const [profile, setProfile] = useState(initial);
  const [form, setForm] = useState({
    licenseNumber: initial?.licenseNumber ?? '',
    bio: initial?.bio ?? '',
    phoneNumber: initial?.phoneNumber ?? '',
    commissionRate: initial?.commissionRate?.toString() ?? '',
    yearsOfExperience: initial?.yearsOfExperience?.toString() ?? '',
    certifications: initial?.certifications ?? '',
  });
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const save = async () => {
    setSaving(true);
    setMessage(null);
    setError(null);
    try {
      const saved = await upsertMyAgentProfile({
        licenseNumber: form.licenseNumber.trim(),
        bio: form.bio.trim(),
        phoneNumber: form.phoneNumber.trim() || undefined,
        commissionRate: form.commissionRate ? Number(form.commissionRate) : undefined,
        yearsOfExperience: form.yearsOfExperience ? Number(form.yearsOfExperience) : undefined,
        certifications: form.certifications.trim() || undefined,
      });
      setProfile(saved);
      setMessage('Profile saved — you now appear in the agents directory.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to save the profile.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      {profile === null && (
        <Card className="border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          You don't have a directory profile yet. Fill this in so tenants can find and book you.
        </Card>
      )}
      {profile && (
        <div className="flex items-center gap-2 text-sm text-muted">
          <Badge tone={profile.status === 0 ? 'green' : 'gray'}>{AGENT_STATUS_LABELS[profile.status] ?? 'Unknown'}</Badge>
          Joined {new Date(profile.joinDate).toLocaleDateString()}
        </div>
      )}
      <Card className="space-y-4 p-6">
        <Field label="License number *">
          <input value={form.licenseNumber} onChange={set('licenseNumber')} className={inputCls} placeholder="e.g. GH-AGT-0000" />
        </Field>
        <Field label="Bio *">
          <textarea value={form.bio} onChange={set('bio')} rows={4} className={inputCls} placeholder="Tell tenants who you are and the areas you cover." />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Phone number">
            <input value={form.phoneNumber} onChange={set('phoneNumber')} className={inputCls} placeholder="+233 …" />
          </Field>
          <Field label="Commission rate (%)">
            <input value={form.commissionRate} onChange={set('commissionRate')} type="number" min="0" step="0.5" className={inputCls} />
          </Field>
          <Field label="Years of experience">
            <input value={form.yearsOfExperience} onChange={set('yearsOfExperience')} type="number" min="0" className={inputCls} />
          </Field>
          <Field label="Certifications">
            <input value={form.certifications} onChange={set('certifications')} className={inputCls} placeholder="Comma-separated" />
          </Field>
        </div>
        {message && <p className="text-sm text-brand">{message}</p>}
        {error && <p className="text-sm text-rose-600" role="alert">{error}</p>}
        <Button disabled={saving || !form.licenseNumber.trim() || !form.bio.trim()} onClick={save}>
          {saving ? 'Saving…' : 'Save profile'}
        </Button>
      </Card>
    </div>
  );
}

const inputCls =
  'w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      {children}
    </label>
  );
}
