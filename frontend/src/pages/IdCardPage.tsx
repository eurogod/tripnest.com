import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { profileApi } from '@/lib/services';
import { api } from '@/lib/api';
import { Button, Spinner } from '@/components/ui';
import { VerifiedBadge, Avatar } from '@/components/badges';
import { Shield, Camera, Doc } from '@/components/icons';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { UserRoleLabel } from '@/lib/enums';

function photoUrl(profile?: Record<string, unknown>): string | null {
  const raw = (profile?.photoPath ?? profile?.profilePhotoPath ?? profile?.selfiePhotoPath) as string | undefined;
  if (!raw) return null;
  if (/^https?:\/\//.test(raw)) return raw;
  return raw.startsWith('/') ? raw : `/${raw}`;
}

export default function IdCardPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [downloading, setDownloading] = useState(false);

  const { data: profile } = useQuery({
    queryKey: ['profile-me'],
    queryFn: profileApi.me,
    enabled: !!user,
  });

  async function download() {
    setDownloading(true);
    try {
      const res = await api.blob(profileApi.idCardUrl());
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `tripnest-id-${user?.tripNestId ?? 'card'}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Your official ID card isn’t ready to download yet.');
    } finally {
      setDownloading(false);
    }
  }

  if (!user) return null;

  if (!user.isVerified) {
    return (
      <div className="container-tn max-w-lg py-16 text-center">
        <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-50 text-brand-600">
          <Shield className="h-7 w-7" />
        </div>
        <h1 className="mt-4 text-2xl font-extrabold">Your TripNest ID isn’t ready yet</h1>
        <p className="mx-auto mt-2 max-w-sm text-sm text-muted">
          Complete Ghana Card identity verification — a live selfie matched against the NIA registry — to unlock your
          digital ID.
        </p>
        <Link to="/verification" className="mt-6 inline-block">
          <Button>Go to verification</Button>
        </Link>
      </div>
    );
  }

  const avatar = photoUrl(profile);

  return (
    <div className="container-tn max-w-xl py-10">
      <h1 className="text-2xl font-extrabold">My TripNest ID</h1>
      <p className="mt-1 text-sm text-muted">
        Your verified digital identity. Show it when meeting hosts, agents or caretakers in person.
      </p>

      {/* Digital ID card */}
      <div className="mt-6 overflow-hidden rounded-2xl bg-gradient-to-br from-brand-700 via-brand-600 to-brand-800 p-6 text-white shadow-lg">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 font-extrabold">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-white/15">
              <Shield className="h-4 w-4" />
            </span>
            <span className="text-lg">
              Trip<span className="text-gold-300">Nest</span> ID
            </span>
          </div>
          <span className="pill bg-white/15 text-white ring-1 ring-white/25">
            <Shield className="h-3.5 w-3.5" /> Verified
          </span>
        </div>

        <div className="mt-6 flex items-center gap-4">
          <div className="rounded-2xl ring-2 ring-white/40">
            <Avatar name={user.fullName} src={avatar} size={72} />
          </div>
          <div className="min-w-0">
            <p className="truncate text-xl font-extrabold">{user.fullName}</p>
            <p className="text-sm text-white/80">{UserRoleLabel[user.role]}</p>
            <p className="mt-1 font-mono text-sm tracking-wider text-gold-300">{user.tripNestId ?? '—'}</p>
          </div>
        </div>

        <div className="mt-6 flex items-end justify-between border-t border-white/15 pt-4 text-xs text-white/70">
          <div>
            <p className="uppercase tracking-wide">Identity</p>
            <p className="font-semibold text-white">Ghana Card · NIA matched</p>
          </div>
          <div className="text-right">
            <p className="uppercase tracking-wide">Status</p>
            <p className="font-semibold text-white">Active</p>
          </div>
        </div>
      </div>

      <div className="mt-5 flex flex-wrap gap-3">
        <Button onClick={download} loading={downloading}>
          {downloading ? <Spinner /> : <Doc className="h-4 w-4" />} Download official PDF
        </Button>
        <Link to="/verification">
          <Button variant="outline">
            <Camera className="h-4 w-4" /> Verification center
          </Button>
        </Link>
      </div>

      <div className="mt-6 flex items-center gap-2 text-sm text-muted">
        <VerifiedBadge size="sm" /> This ID confirms email, phone and Ghana Card checks all passed.
      </div>
    </div>
  );
}
