import { useEffect, useState } from 'react';
import {
  getVerificationStatus, startVerification, type VerificationStatus, type VerificationState,
} from '../api/verification';
import { ApiError } from '../api/client';
import { formatIsoDate } from '../api/backend';
import { refreshSessionFromServer, useSession } from '../store/authStore';
import { useAsync } from '../hooks/useAsync';
import Badge from './ui/Badge';
import SelfieCapture from './SelfieCapture';
import { ShieldIcon } from './tenant/icons';

const INPUT =
  'w-full rounded-xl border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-ink outline-none focus:border-brand';

const POLL_MS = 5000;

// Card IDs as issued by the TripNest.Id authority (see the trip-card console's
// nextCardId): GHA-{issue year}-{6-digit serial}, e.g. GHA-2026-000123.
const CARD_ID_PATTERN = /^GHA-\d{4}-\d{6}$/;

/**
 * Ghana Card identity verification, shared by landlord Settings and the
 * standalone /get-verified page. Submits selfie + card details to
 * api/verification and polls while the async check runs server-side; refreshes
 * the session (isVerified flag) when the check lands on Verified.
 */
export default function IdentityVerification({
  onStateChange,
  onSkip
}: {
  /** Reports every observed state so hosts can react (e.g. unlock a Continue button). */
  onStateChange?: (state: VerificationState) => void;
   onSkip?: () => void;
}) {
  const session = useSession();
  const initial = useAsync(getVerificationStatus, []);
  const [override, setOverride] = useState<VerificationStatus | null>(null);
  const status = override ?? initial.data ?? null;
  const state: VerificationState = status?.state ?? 'not-started';

  useEffect(() => {
    onStateChange?.(initial.loading && !override ? 'not-started' : state);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state, initial.loading]);

  const [sessionFirst = '', ...sessionRest] = (session?.name ?? '').split(' ');
  const [firstName, setFirstName] = useState(sessionFirst);
  const [lastName, setLastName] = useState(sessionRest.join(' '));
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [cardNumber, setCardNumber] = useState('');
  const [selfie, setSelfie] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The check resolves in the background — poll until it leaves Pending.
  useEffect(() => {
    if (state !== 'pending') return;
    const id = setInterval(async () => {
      try {
        const next = await getVerificationStatus();
        if (next && next.state !== 'pending') {
          setOverride(next);
          // Pull the refreshed isVerified flag so gated pages unlock.
          if (next.state === 'verified') void refreshSessionFromServer();
        }
      } catch { /* transient — keep polling */ }
    }, POLL_MS);
    return () => clearInterval(id);
  }, [state]);

  const cardNumberValid = CARD_ID_PATTERN.test(cardNumber.trim().toUpperCase());
  const canSubmit = !submitting
    && firstName.trim().length > 0
    && lastName.trim().length > 0
    && dateOfBirth.length > 0
    && cardNumberValid
    && selfie !== null;

  const submit = async () => {
    if (!canSubmit || !selfie) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await startVerification({
        ghanaCardNumber: cardNumber.trim().toUpperCase(),
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        dateOfBirth,
        selfie,
      });
      setOverride(result);
    } catch (err) {
      // The server keeps one attempt per card number (unique index) and 500s on
      // a duplicate insert — the common case is a retry reusing the same card.
      if (err instanceof ApiError && err.statusCode === 500 && state === 'rejected') {
        setError('This card number already has a recorded attempt and the server can’t accept another yet. Please contact support to retry.');
      } else {
        setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (initial.loading && !override) {
    return <p className="text-sm text-muted">Checking your verification status…</p>;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        {state === 'verified' && <Badge tone="green">✓ Verified</Badge>}
        {state === 'pending' && <Badge tone="amber">Under review</Badge>}
        {state === 'rejected' && <Badge tone="red">Rejected</Badge>}
      </div>

      {state === 'verified' && status && (
        <div className="flex items-center gap-4 rounded-2xl border border-gray-200 px-5 py-4">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-50 text-brand">
                 <ShieldIcon size={19} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-ink">Ghana Card {status.ghanaCardNumber}</p>
            <p className="text-sm text-muted">
              Your identity is verified{status.reviewedAt ? ` — confirmed ${formatIsoDate(status.reviewedAt)}` : ''}.
            </p>
          </div>
        </div>
      )}

      {state === 'pending' && (
        <div className="rounded-2xl border border-amber-200 bg-amber-50 px-5 py-4 text-sm text-amber-800">
          <p className="font-semibold">We're checking your details.</p>
          <p className="mt-0.5 border border-black">
            Your Ghana Card and selfie are being matched against the national registry.
            This usually takes under a minute — the result will appear here.
          </p>
        </div>
      )}

      {(state === 'not-started' || state === 'rejected') && (
        <div className="space-y-4">
          {state === 'rejected' && (
            <div className="rounded-2xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm text-rose-700">
              <p className="font-semibold">Your last attempt was rejected.</p>
              {status?.failureReason && <p className="mt-0.5">{status.failureReason}</p>}
              <p className="mt-0.5">Check your details below and try again.</p>
            </div>
          )}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">First name (as on your card)</span>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value)} className={INPUT} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">Last name (as on your card)</span>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)} className={INPUT} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">TripNest Card number</span>
              <input
                value={cardNumber}
                onChange={(e) => setCardNumber(e.target.value)}
                placeholder="GHA-2026-000123"
                className={INPUT}
              />
              {cardNumber.trim().length > 0 && !cardNumberValid && (
                <span className="mt-1 block text-xs text-rose-600">
                  Card numbers look like GHA-2026-000123 (year, then 6-digit serial).
                </span>
              )}
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-muted">Date of birth</span>
              <input
                type="date"
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
                max={new Date().toISOString().slice(0, 10)}
                className={INPUT}
              />
            </label>
          </div>
          <SelfieCapture onCapture={setSelfie} />
          {error && <p className="text-sm text-rose-600" role="alert">{error}</p>}
          <button
            type="button"
            onClick={() => void submit()}
            disabled={!canSubmit}
            className="inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-40"
          >
            <ShieldIcon size={15} />
            {submitting ? 'Submitting…' : state === 'rejected' ? 'Try again' : 'Verify my identity'}
         </button>

        </div>
      )}

                {onSkip && (
                    <button
                    type="button"
                    onClick={onSkip}
                    disabled={submitting}
                    className="text-sm font-semibold text-muted transition-colors hover:text-ink disabled:opacity-40"
                    >
                    Skip for now
                    </button>
                )}
    </div>
  );
}
