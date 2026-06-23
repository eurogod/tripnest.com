import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { authApi, profileApi, verificationApi } from '@/lib/services';
import { Button, Input, Spinner } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { OtpInput } from '@/components/OtpInput';
import { SelfieCapture } from '@/components/SelfieCapture';
import { VerifiedBadge } from '@/components/badges';
import { Check, Mail, Shield, Camera, X } from '@/components/icons';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';
import { VerificationStatus } from '@/lib/enums';

function useCooldown() {
  const [left, setLeft] = useState(0);
  const timer = useRef<number>();
  const start = (secs: number) => {
    setLeft(secs);
    window.clearInterval(timer.current);
    timer.current = window.setInterval(() => {
      setLeft((l) => {
        if (l <= 1) window.clearInterval(timer.current);
        return l - 1;
      });
    }, 1000);
  };
  useEffect(() => () => window.clearInterval(timer.current), []);
  return { left, start };
}

export default function VerificationPage() {
  const { user, patchUser } = useAuth();

  return (
    <div className="container-tn max-w-3xl py-8">
      <h1 className="text-2xl font-extrabold">Verification center</h1>
      <p className="mt-1 text-sm text-muted">
        Three independent checks build your TripNest trust. Hosts, agents and caretakers must complete identity
        verification before they can list or accept work.
      </p>

      <div className="mt-6 space-y-4">
        <ContactTrack
          kind="email"
          done={!!user?.emailVerified}
          onVerified={() => patchUser({ emailVerified: true })}
        />
        <ContactTrack
          kind="phone"
          done={!!user?.phoneVerified}
          onVerified={() => patchUser({ phoneVerified: true })}
        />
        <IdentityTrack done={!!user?.isVerified} onVerified={(tnid) => patchUser({ isVerified: true, tripNestId: tnid })} />
      </div>
    </div>
  );
}

function TrackShell({
  icon,
  title,
  desc,
  done,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  desc: string;
  done: boolean;
  children?: React.ReactNode;
}) {
  return (
    <div className="card p-5">
      <div className="flex items-start gap-4">
        <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${done ? 'bg-success/10 text-success' : 'bg-brand-50 text-brand-600'}`}>
          {done ? <Check className="h-5 w-5" /> : icon}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="font-bold">{title}</h3>
            {done && <VerifiedBadge size="sm" label="Verified" />}
          </div>
          <p className="mt-0.5 text-sm text-muted">{desc}</p>
          {!done && <div className="mt-3">{children}</div>}
        </div>
      </div>
    </div>
  );
}

function ContactTrack({ kind, done, onVerified }: { kind: 'email' | 'phone'; done: boolean; onVerified: () => void }) {
  const toast = useToast();
  const cooldown = useCooldown();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState('');
  const [sending, setSending] = useState(false);
  const [verifying, setVerifying] = useState(false);

  const isEmail = kind === 'email';

  async function send() {
    setSending(true);
    try {
      await (isEmail ? authApi.sendEmailOtp() : authApi.sendPhoneOtp());
      toast.success(`Code sent to your ${kind}`);
      setOpen(true);
      cooldown.start(60);
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        toast.error(err.message || 'Please wait before requesting another code');
        cooldown.start(60);
        setOpen(true);
      } else {
        toast.error(err instanceof ApiError ? err.message : 'Could not send code');
      }
    } finally {
      setSending(false);
    }
  }

  async function verify() {
    setVerifying(true);
    try {
      await (isEmail ? authApi.verifyEmailOtp(code) : authApi.verifyPhoneOtp(code));
      toast.success(`${isEmail ? 'Email' : 'Phone'} verified!`);
      setOpen(false);
      setCode('');
      onVerified();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Invalid code');
    } finally {
      setVerifying(false);
    }
  }

  return (
    <TrackShell
      icon={isEmail ? <Mail className="h-5 w-5" /> : <Shield className="h-5 w-5" />}
      title={isEmail ? 'Email address' : 'Phone number'}
      desc={isEmail ? 'Confirm you own this email — used for receipts & alerts.' : 'Confirm your phone — used for SMS safety alerts & OTP.'}
      done={done}
    >
      <Button size="sm" loading={sending} onClick={send}>
        Verify {kind}
      </Button>

      <Modal open={open} onClose={() => setOpen(false)} title={`Verify your ${kind}`}>
        <p className="text-sm text-muted">Enter the 6-digit code we sent to your {kind}.</p>
        <div className="mt-4">
          <OtpInput value={code} onChange={setCode} />
        </div>
        <Button block className="mt-5" loading={verifying} disabled={code.length < 6} onClick={verify}>
          Confirm
        </Button>
        <button
          disabled={cooldown.left > 0 || sending}
          onClick={send}
          className="mt-3 w-full text-center text-sm font-semibold text-brand-700 disabled:text-muted"
        >
          {cooldown.left > 0 ? `Resend in ${cooldown.left}s` : 'Resend code'}
        </button>
      </Modal>
    </TrackShell>
  );
}

function IdentityTrack({ done, onVerified }: { done: boolean; onVerified: (tnid?: string) => void }) {
  const toast = useToast();
  const [open, setOpen] = useState(false);
  const [step, setStep] = useState<'form' | 'selfie' | 'pending'>('form');
  const [form, setForm] = useState({ ghanaCardNumber: '', firstName: '', lastName: '', dateOfBirth: '' });
  const [selfie, setSelfie] = useState<{ file: File; url: string } | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Poll status while pending (or to reflect any prior submission).
  const { data: status } = useQuery({
    queryKey: ['verification-status'],
    queryFn: verificationApi.status,
    enabled: open && step === 'pending',
    refetchInterval: (q) => (q.state.data?.status === VerificationStatus.Pending ? 3000 : false),
  });

  useEffect(() => {
    if (status?.status === VerificationStatus.Verified) {
      toast.success('Identity verified! Your TripNest ID is ready.');
      onVerified();
      setOpen(false);
    } else if (status?.status === VerificationStatus.Rejected) {
      toast.error(status.failureReason || 'Verification was rejected. You can try again.');
      setStep('form');
    }
  }, [status, onVerified, toast]);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  async function submit() {
    if (!selfie) return;
    setSubmitting(true);
    try {
      const { photoPath } = await profileApi.uploadPhoto(selfie.file);
      await verificationApi.start({ ...form, selfiePhotoPath: photoPath });
      toast.info('Submitted — verifying your identity…');
      setStep('pending');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Could not submit verification');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <TrackShell
      icon={<Shield className="h-5 w-5" />}
      title="Ghana Card identity"
      desc="Verify with your Ghana Card + a live selfie. Unlocks listing, agreements and your TripNest ID."
      done={done}
    >
      <Button size="sm" onClick={() => { setOpen(true); setStep('form'); }}>
        Start identity verification
      </Button>

      <Modal open={open} onClose={() => setOpen(false)} title="Ghana Card verification" maxWidth="max-w-md">
        {step === 'form' && (
          <div className="space-y-4">
            <Input label="Ghana Card number" placeholder="GHA-XXXXXXXXX-X" value={form.ghanaCardNumber} onChange={set('ghanaCardNumber')} />
            <div className="grid grid-cols-2 gap-3">
              <Input label="First name" value={form.firstName} onChange={set('firstName')} />
              <Input label="Last name" value={form.lastName} onChange={set('lastName')} />
            </div>
            <Input label="Date of birth" type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} />
            <Button
              block
              disabled={!form.ghanaCardNumber || !form.firstName || !form.lastName || !form.dateOfBirth}
              onClick={() => setStep('selfie')}
            >
              Next: take a selfie
            </Button>
          </div>
        )}

        {step === 'selfie' && (
          <div>
            <p className="mb-4 text-center text-sm text-muted">
              Center your face in the circle. We compare it with your Ghana Card photo via the NIA registry.
            </p>
            <SelfieCapture onCapture={(file, url) => setSelfie({ file, url })} />
            <Button block className="mt-5" loading={submitting} disabled={!selfie} onClick={submit}>
              Submit for verification
            </Button>
            <button onClick={() => setStep('form')} className="mt-2 w-full text-center text-sm font-semibold text-muted">
              Back
            </button>
          </div>
        )}

        {step === 'pending' && (
          <div className="py-6 text-center">
            {status?.status === VerificationStatus.Rejected ? (
              <>
                <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-danger/10 text-danger">
                  <X className="h-7 w-7" />
                </div>
                <h3 className="mt-3 font-bold">Verification rejected</h3>
                <p className="mt-1 text-sm text-muted">{status.failureReason || 'Please try again with a clearer selfie.'}</p>
                <Button className="mt-4" onClick={() => setStep('form')}>Try again</Button>
              </>
            ) : (
              <>
                <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-50 text-brand-600">
                  <Spinner className="h-7 w-7" />
                </div>
                <h3 className="mt-3 font-bold">Verifying your identity…</h3>
                <p className="mt-1 text-sm text-muted">
                  We're checking the NIA registry and matching your selfie. This usually takes under a minute — you can
                  safely close this and come back.
                </p>
                {status?.faceMatchScore != null && (
                  <p className="mt-2 text-xs text-muted">Face match score: {Math.round(status.faceMatchScore * 100)}%</p>
                )}
              </>
            )}
          </div>
        )}
      </Modal>

      {done && (
        <Link to="/id-card" className="mt-3 inline-flex items-center gap-1.5 text-sm font-bold text-brand-700">
          <Camera className="h-4 w-4" /> View my TripNest ID
        </Link>
      )}
    </TrackShell>
  );
}
