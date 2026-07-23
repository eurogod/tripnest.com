import { useEffect, useRef, useState } from 'react';
import { useSession, updateSession } from '../../store/authStore';
import { getMyProfile, updateMyProfile, uploadProfilePhoto } from '../../api/profile';
import { cacheProfilePhoto, getCachedProfilePhoto } from '../../lib/profilePhoto';
import { ApiError, assetUrl } from '../../api/client';
import Card from '../../components/ui/Card';
import SignatureCard from '../../components/profile/SignatureCard';
import StudentCard from '../../components/profile/StudentCard';
import LoyaltyCard from '../../components/profile/LoyaltyCard';
import Button from '../../components/ui/Button';
import Avatar from '../../components/ui/Avatar';
import Badge from '../../components/ui/Badge';
import ContactVerification from '../../components/ContactVerification';
import { useT } from '../../lib/i18n';
import {
  ShieldIcon, CheckIcon, StarIcon, MapPinIcon, MailIcon, PhoneIcon, BadgeIcon,
} from '../../components/tenant/icons';

function Field({ label, value, onChange, type = 'text', disabled = false, hint }: {
  label: string; value: string; onChange: (v: string) => void; type?: string; disabled?: boolean; hint?: string;
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      <input
        type={type}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className={`w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm text-ink outline-none focus:border-brand ${disabled ? 'bg-gray-50 text-muted' : ''}`}
      />
      {hint && <span className="mt-1 block text-xs text-muted">{hint}</span>}
    </label>
  );
}

function InfoItem({ icon, label }: { icon: React.ReactNode; label: string }) {
  return (
    <div className="flex items-center gap-3 py-2">
      <span className="text-muted">{icon}</span>
      <span className="text-sm text-ink">{label}</span>
    </div>
  );
}

function VerifiedChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-50 px-3 py-1.5 text-sm font-medium text-brand">
      <CheckIcon size={14} /> {label}
    </span>
  );
}

export default function ProfilePage() {
  const session = useSession();
  const t = useT();
  const roleLabel = session ? session.role[0].toUpperCase() + session.role.slice(1) : 'Guest';

  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [photoPath, setPhotoPath] = useState<string | null>(null);
  const [photoBusy, setPhotoBusy] = useState(false);
  const photoRef = useRef<HTMLInputElement>(null);
  const baseName = session?.name ?? 'Guest';
  // Location/work/languages are local flourishes with no backend field — they
  // start empty and only render once the user fills them in.
  const [form, setForm] = useState({
    name: baseName,
    email: session?.email ?? '',
    phone: '',
    location: '',
    work: '',
    languages: '',
    bio: '',
  });

  // Seed the editable fields from the live profile (phone/bio live server-side).
  useEffect(() => {
    getMyProfile()
      .then((me) => {
        setPhotoPath(me.profilePhotoPath ?? null);
        setForm((f) => ({
          ...f,
          name: me.fullName || f.name,
          email: me.email || f.email,
          phone: me.phone ?? '',
          bio: me.bio || f.bio,
        }));
      })
      .catch(() => {});
  }, []);

  const set = (key: keyof typeof form) => (value: string) => {
    setForm((f) => ({ ...f, [key]: value }));
    setSaved(false);
  };

  const firstName = form.name.split(' ')[0];

  const pickPhoto = async (file: File | undefined) => {
    if (!file || photoBusy) return;
    setPhotoBusy(true);
    try {
      const path = await uploadProfilePhoto(file);
      // Core doesn't serve /uploads yet — keep a local copy so the avatar shows it.
      if (session) await cacheProfilePhoto(session.userId, file);
      setPhotoPath(path);
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Could not upload that photo.');
    } finally {
      setPhotoBusy(false);
      if (photoRef.current) photoRef.current.value = '';
    }
  };

  // Location/work/languages are display-only flourishes with no backend home;
  // name, phone and bio persist through PUT /api/profile/me.
  const save = async () => {
    if (saving) return;
    setSaving(true);
    setSaveError('');
    try {
      await updateMyProfile({
        fullName: form.name.trim() || undefined,
        phone: form.phone.trim() || undefined,
        bio: form.bio.trim() || undefined,
      });
      if (session) updateSession({ name: form.name.trim() || session.name });
      setEditing(false);
      setSaved(true);
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : 'Could not save your profile.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="max-w-4xl">
      {/* Cover + identity header */}
      <div>
        <div className="relative h-40 overflow-hidden rounded-xl bg-gradient-to-r from-brand to-emerald-700 sm:h-48">
          <div className="absolute -right-10 -top-16 h-56 w-56 rounded-full bg-white/10" />
          <div className="absolute -bottom-20 right-24 h-48 w-48 rounded-full bg-white/5" />
          <div className="absolute -left-12 -bottom-24 h-56 w-56 rounded-full bg-white/5" />
        </div>

        <div className="px-4 sm:px-8">
          <div className="relative -mt-14 w-fit">
            <input
              ref={photoRef}
              type="file"
              accept="image/png,image/jpeg"
              className="hidden"
              onChange={(e) => void pickPhoto(e.target.files?.[0])}
            />
            <button
              type="button"
              onClick={() => photoRef.current?.click()}
              title="Change photo"
              aria-label="Change profile photo"
              className={`tn-glow block rounded-full ${photoBusy ? 'opacity-60' : ''}`}
            >
              <Avatar
                name={form.name}
                src={getCachedProfilePhoto(session?.userId) ?? (photoPath ? assetUrl(photoPath) : null)}
                size={112}
                className="text-3xl ring-4 ring-white"
              />
            </button>
            <span className="absolute bottom-1 right-1 flex h-8 w-8 items-center justify-center rounded-full border-2 border-white bg-brand text-white">
              <ShieldIcon size={15} />
            </span>
          </div>

          <div className="mt-3 flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-2xl font-bold text-ink sm:text-3xl">{form.name}</h1>
                {session?.verified ? (
                  <Badge tone="green">✓ Verified</Badge>
                ) : (
                  <Badge tone="gray">Not verified</Badge>
                )}
              </div>
              <p className="mt-0.5 text-muted">{roleLabel}{form.location ? ` · ${form.location}` : ''}</p>
              {session?.tripNestId && (
                <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink">
                  <span className="flex items-center gap-1 text-muted">
                    <ShieldIcon size={14} /> TripNest ID {session.tripNestId}
                  </span>
                </div>
              )}
              {saved && <p className="mt-2 text-sm font-medium text-brand">Profile updated</p>}
            </div>
            {!editing && (
              <Button onClick={() => { setEditing(true); setSaved(false); }}>
                {t('Edit profile')}
              </Button>
            )}
          </div>
        </div>
      </div>

      <div className="mt-8 space-y-6">
        {editing ? (
          <Card className="p-6">
            <h2 className="mb-4 text-xl font-bold text-ink">Edit your profile</h2>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Full name" value={form.name} onChange={set('name')} />
              <Field label="Email" type="email" value={form.email} onChange={set('email')} disabled hint="Your sign-in email can't be changed here." />
              <Field label="Phone" value={form.phone} onChange={set('phone')} />
              <Field label="Location" value={form.location} onChange={set('location')} />
              <Field label="Work" value={form.work} onChange={set('work')} />
              <Field label="Languages" value={form.languages} onChange={set('languages')} />
            </div>
            <label className="mt-4 block">
              <span className="mb-1.5 block text-sm font-medium text-ink">About you</span>
              <textarea
                value={form.bio}
                onChange={(e) => set('bio')(e.target.value)}
                rows={4}
                className="w-full resize-none rounded-lg border border-gray-200 px-3 py-2.5 text-sm text-ink outline-none focus:border-brand"
              />
            </label>
            {saveError && <p className="mt-3 text-sm text-rose-600" role="alert">{saveError}</p>}
            <div className="mt-5 flex gap-2">
              <Button onClick={() => void save()} disabled={saving}>{saving ? 'Saving…' : t('Save changes')}</Button>
              <Button variant="ghost" onClick={() => setEditing(false)}>Cancel</Button>
            </div>
          </Card>
        ) : (
          <>
            <SignatureCard />

            <Card className="p-6">
              <h2 className="text-xl font-bold text-ink">About {firstName}</h2>
              <p className="mt-3 text-ink">
                {form.bio || 'No bio yet — use “Edit profile” to introduce yourself.'}
              </p>
              <div className="mt-4 grid grid-cols-1 gap-x-8 border-t border-gray-100 pt-3 sm:grid-cols-2">
                {form.location && <InfoItem icon={<MapPinIcon size={18} />} label={`Lives in ${form.location}`} />}
                {form.languages && <InfoItem icon={<BadgeIcon size={18} />} label={`Speaks ${form.languages}`} />}
                {form.work && <InfoItem icon={<ShieldIcon size={18} />} label={form.work} />}
                {form.email && <InfoItem icon={<MailIcon size={18} />} label={form.email} />}
                {form.phone && <InfoItem icon={<PhoneIcon size={18} />} label={form.phone} />}
              </div>
            </Card>

            <Card className="p-6">
              <h2 className="flex items-center gap-2 text-lg font-bold text-ink">
                <ShieldIcon size={18} className="text-brand" /> {firstName}'s confirmed information
              </h2>
              <div className="mt-4 flex flex-wrap items-center gap-2">
                {session?.verified ? (
                  <VerifiedChip label="Identity" />
                ) : (
                  <Badge tone="gray">Identity not verified</Badge>
                )}
                {session?.emailVerified ? (
                  <VerifiedChip label="Email address" />
                ) : (
                  <ContactVerification kind="email" verified={false} />
                )}
                {session?.phoneVerified ? (
                  <VerifiedChip label="Phone number" />
                ) : (
                  <ContactVerification kind="phone" verified={false} />
                )}
              </div>
            </Card>

            <StudentCard />
            <LoyaltyCard />

            <section>
              <h2 className="mb-4 flex items-center gap-2 text-lg font-bold text-ink">
                <StarIcon size={18} className="text-amber-400" /> What hosts say about {firstName}
              </h2>
              <Card className="border-dashed p-8 text-center">
                <p className="text-sm text-muted">
                  No host reviews yet — they appear here after your completed stays.
                </p>
              </Card>
            </section>
          </>
        )}
      </div>
    </div>
  );
}
