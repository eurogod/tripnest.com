import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import IdentityVerification from '../../components/IdentityVerification';
import { getMyProfile, updateMyProfile, uploadProfilePhoto } from '../../api/profile';
import { cacheProfilePhoto, getCachedProfilePhoto } from '../../lib/profilePhoto';
import { changePassword, deleteAccount } from '../../api/settings';
import { ApiError, assetUrl } from '../../api/client';
import Avatar from '../../components/ui/Avatar';
import ContactVerification from '../../components/ContactVerification';
import { LogOutIcon, MailIcon, TrashIcon } from '../../components/tenant/icons';
import { useAsync } from '../../hooks/useAsync';
import { signOut, updateSession, useSession } from '../../store/authStore';

const INPUT =
  'w-full rounded-xl border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-ink outline-none focus:border-brand';
const BTN_OUTLINE =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink transition-colors hover:bg-gray-50';

const LockIcon = ({ size = 15 }: { size?: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
  </svg>
);

const EyeIcon = ({ size = 16, off = false }: { size?: number; off?: boolean }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
    <circle cx="12" cy="12" r="3" />
    {off && <line x1="3" y1="3" x2="21" y2="21" />}
  </svg>
);

const GoogleIcon = ({ size = 22 }: { size?: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true">
    <path fill="#4285F4" d="M23.5 12.27c0-.85-.08-1.66-.22-2.45H12v4.64h6.45a5.52 5.52 0 0 1-2.4 3.62v3h3.88c2.27-2.09 3.57-5.17 3.57-8.81z" />
    <path fill="#34A853" d="M12 24c3.24 0 5.96-1.07 7.94-2.91l-3.88-3.01c-1.08.72-2.45 1.15-4.06 1.15-3.13 0-5.78-2.11-6.72-4.95H1.27v3.11A12 12 0 0 0 12 24z" />
    <path fill="#FBBC05" d="M5.28 14.28A7.2 7.2 0 0 1 4.9 12c0-.79.14-1.56.38-2.28V6.61H1.27a12 12 0 0 0 0 10.78l4.01-3.11z" />
    <path fill="#EA4335" d="M12 4.77c1.76 0 3.35.61 4.6 1.8l3.44-3.44A11.97 11.97 0 0 0 12 0 12 12 0 0 0 1.27 6.61l4.01 3.11C6.22 6.88 8.87 4.77 12 4.77z" />
  </svg>
);

const AnalyticsIcon = ({ size = 22 }: { size?: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <rect x="4" y="13" width="4" height="7" rx="1.2" fill="#E37400" />
    <rect x="10" y="8" width="4" height="12" rx="1.2" fill="#F9AB00" />
    <rect x="16" y="3" width="4" height="17" rx="1.2" fill="#F9AB00" />
  </svg>
);

function SectionHeading({ title, desc }: { title: string; desc: string }) {
  return (
    <div className="mb-5">
      <h2 className="text-base font-semibold text-ink">{title}</h2>
      <p className="mt-0.5 text-sm text-muted">{desc}</p>
    </div>
  );
}

function PasswordField({
  label, value, onChange,
}: { label: string; value: string; onChange: (v: string) => void }) {
  const [show, setShow] = useState(false);
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm text-muted">{label}</span>
      <div className="flex items-center rounded-xl border border-gray-200 bg-white px-3.5 focus-within:border-brand">
        <span className="text-muted"><LockIcon /></span>
        <input
          type={show ? 'text' : 'password'}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="w-full bg-transparent px-2.5 py-2.5 text-sm text-ink outline-none"
        />
        <button
          type="button"
          onClick={() => setShow((v) => !v)}
          className="text-muted transition-colors hover:text-ink"
          aria-label={show ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
        >
          <EyeIcon off={show} />
        </button>
      </div>
    </label>
  );
}

/** Change-password form against PUT /api/settings/password. */
function PasswordSection() {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (saving) return;
    if (next !== confirm) {
      setMessage({ ok: false, text: 'New passwords don’t match.' });
      return;
    }
    setSaving(true);
    setMessage(null);
    try {
      await changePassword({ currentPassword: current, newPassword: next, confirmNewPassword: confirm });
      setCurrent(''); setNext(''); setConfirm('');
      setMessage({ ok: true, text: 'Password updated.' });
    } catch (err) {
      setMessage({ ok: false, text: err instanceof ApiError ? err.message : 'Could not update your password.' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="py-8">
      <SectionHeading title="Password" desc="Modify your current password." />
      <form onSubmit={submit} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <PasswordField label="Current password" value={current} onChange={setCurrent} />
          <PasswordField label="New password" value={next} onChange={setNext} />
          <PasswordField label="Confirm new password" value={confirm} onChange={setConfirm} />
        </div>
        {message && (
          <p className={`text-sm ${message.ok ? 'text-brand' : 'text-rose-600'}`} role={message.ok ? undefined : 'alert'}>
            {message.text}
          </p>
        )}
        <button
          type="submit"
          disabled={saving || !current || !next || !confirm}
          className="inline-flex items-center justify-center rounded-xl bg-brand px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-40"
        >
          {saving ? 'Updating…' : 'Update password'}
        </button>
      </form>
    </section>
  );
}

function IntegrationRow({
  icon,
  title,
  desc,
}: {
  icon: React.ReactNode;
  title: string;
  desc: string;
}) {
  return (
    <div className="flex items-center gap-4 rounded-2xl border border-gray-200 px-5 py-4">
      <span className="flex h-10 w-10 shrink-0 items-center justify-center">{icon}</span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-semibold text-ink">{title}</span>
        <span className="block truncate text-sm text-muted">{desc}</span>
      </span>
      <span className="shrink-0 rounded-full border border-brand px-4 py-1.5 text-xs font-semibold text-brand">
        Connected
      </span>
    </div>
  );
}

/**
 * Ghana Card identity verification (compulsory for hosts — listing/calendar
 * actions 403 until verified). The flow itself lives in the shared
 * IdentityVerification component, also used by /get-verified.
 */
function VerificationSection() {
  return (
    <section className="py-8">
      <SectionHeading
        title="Identity verification"
        desc="Hosts must verify with their Ghana Card before listing properties."
      />
      <IdentityVerification />
    </section>
  );
}

export default function LandlordSettingsPage() {
  const navigate = useNavigate();
  const session = useSession();
  const profile = useAsync(getMyProfile, []);

  const [firstInitial = '', ...restName] = (session?.name ?? '').split(' ');
  const [firstName, setFirstName] = useState(firstInitial);
  const [lastName, setLastName] = useState(restName.join(' '));

  // Profile photo — uploaded through POST /api/profile/photo. The stored path
  // is served via /uploads once Core exposes it; Avatar falls back to initials.
  const [photoPath, setPhotoPath] = useState<string | null>(null);
  const [photoBusy, setPhotoBusy] = useState(false);
  const [photoError, setPhotoError] = useState('');
  const photoRef = useRef<HTMLInputElement>(null);
  const effectivePhoto = photoPath ?? profile.data?.profilePhotoPath ?? null;

  const pickPhoto = async (file: File | undefined) => {
    if (!file || photoBusy) return;
    setPhotoBusy(true);
    setPhotoError('');
    try {
      const path = await uploadProfilePhoto(file);
      // Core doesn't serve /uploads yet — keep a local copy so the avatar shows it.
      if (session) await cacheProfilePhoto(session.userId, file);
      setPhotoPath(path);
    } catch (err) {
      setPhotoError(err instanceof ApiError ? err.message : 'Could not upload that picture.');
    } finally {
      setPhotoBusy(false);
      if (photoRef.current) photoRef.current.value = '';
    }
  };

  // Full name — saved through PUT /api/profile/me.
  const [nameSaving, setNameSaving] = useState(false);
  const [nameStatus, setNameStatus] = useState<{ ok: boolean; text: string } | null>(null);

  const saveName = async () => {
    const fullName = `${firstName.trim()} ${lastName.trim()}`.trim();
    if (!fullName || nameSaving) return;
    setNameSaving(true);
    setNameStatus(null);
    try {
      await updateMyProfile({ fullName });
      updateSession({ name: fullName });
      setNameStatus({ ok: true, text: 'Name updated.' });
    } catch (err) {
      setNameStatus({ ok: false, text: err instanceof ApiError ? err.message : 'Could not save your name.' });
    } finally {
      setNameSaving(false);
    }
  };

  const removeAccount = async () => {
    if (!window.confirm('Delete your TripNest account? This deactivates it permanently.')) return;
    try {
      await deleteAccount();
      signOut();
      navigate('/welcome');
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'Could not delete your account.');
    }
  };

  return (
    <div className="mx-auto max-w-3xl">
      <div className="border-b border-gray-200 pb-6">
        <h1 className="text-xl font-semibold text-ink">Account</h1>
        <p className="mt-0.5 text-sm text-muted">
          Real-time information and activities of your property.
        </p>
      </div>

      <div className="divide-y divide-gray-200">
        {/* Profile picture */}
        <section className="flex flex-wrap items-center gap-5 py-8">
          <Avatar
            name={session?.name ?? 'TripNest Host'}
            src={getCachedProfilePhoto(session?.userId) ?? (effectivePhoto ? assetUrl(effectivePhoto) : null)}
            size={88}
          />
          <div className="min-w-0 flex-1">
            <p className="font-semibold text-ink">Profile picture</p>
            <p className="text-sm text-muted">PNG, JPEG under 15MB</p>
            {photoError && <p className="mt-1 text-sm text-rose-600" role="alert">{photoError}</p>}
            {!photoError && photoPath && <p className="mt-1 text-sm text-brand">Picture uploaded.</p>}
          </div>
          <div className="flex shrink-0 gap-2">
            <input
              ref={photoRef}
              type="file"
              accept="image/png,image/jpeg"
              className="hidden"
              onChange={(e) => void pickPhoto(e.target.files?.[0])}
            />
            <button type="button" className={BTN_OUTLINE} disabled={photoBusy} onClick={() => photoRef.current?.click()}>
              {photoBusy ? 'Uploading…' : 'Upload new picture'}
            </button>
          </div>
        </section>

        {/* Full name */}
        <section className="py-8">
          <h2 className="mb-4 text-base font-semibold text-ink">Full name</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">First name</span>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value)} className={INPUT} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">Last name</span>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)} className={INPUT} />
            </label>
          </div>
          <div className="mt-4 flex items-center gap-3">
            <button type="button" className={BTN_OUTLINE} disabled={nameSaving} onClick={() => void saveName()}>
              {nameSaving ? 'Saving…' : 'Save name'}
            </button>
            {nameStatus && (
              <span className={`text-sm ${nameStatus.ok ? 'text-brand' : 'text-rose-600'}`}>{nameStatus.text}</span>
            )}
          </div>
        </section>

        {/* Contact email */}
        <section className="py-8">
          <SectionHeading title="Contact email" desc="Your sign-in email — contact support to change it." />
          <div className="flex flex-wrap items-center justify-between gap-4">
            <label className="block w-full sm:max-w-sm">
              <span className="mb-1.5 block text-sm text-muted">Email</span>
              <div className="flex items-center rounded-xl border border-gray-200 bg-gray-50 px-3.5">
                <span className="text-muted"><MailIcon size={15} /></span>
                <input
                  type="email"
                  value={session?.email ?? ''}
                  readOnly
                  className="w-full bg-transparent px-2.5 py-2.5 text-sm text-ink outline-none"
                />
              </div>
            </label>
            <ContactVerification kind="email" verified={session?.emailVerified ?? false} />
          </div>
        </section>

        {/* Password */}
        <PasswordSection />

        {/* Identity verification */}
        <VerificationSection />

        {/* Integrated account */}
        <section className="py-8">
          <SectionHeading title="Integrated account" desc="Manage your current integrated accounts." />
          <div className="space-y-3">
            <IntegrationRow
              icon={<AnalyticsIcon />}
              title="Google analytics"
              desc="Navigate the Google Analytics interface and reports."
            />
            <IntegrationRow
              icon={<GoogleIcon />}
              title="Google"
              desc="Use Google for the faster login methods in your account."
            />
          </div>
        </section>

        {/* Account security */}
        <section className="py-8">
          <SectionHeading title="Account security" desc="Manage your account security." />
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              className={BTN_OUTLINE}
              onClick={() => {
                signOut();
                navigate('/welcome');
              }}
            >
              <LogOutIcon size={15} /> Log out
            </button>
            <button
              type="button"
              onClick={() => void removeAccount()}
              className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm font-semibold text-rose-600 transition-colors hover:bg-rose-50"
            >
              <TrashIcon size={15} /> Delete my account
            </button>
          </div>
        </section>
      </div>
    </div>
  );
}
