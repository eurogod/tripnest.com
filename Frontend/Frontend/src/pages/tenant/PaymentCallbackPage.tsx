import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  ESCROW_STATUS,
  clearPendingCheckout,
  getEscrow,
  readPendingCheckout,
} from '../../api/escrow';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import { formatCedi } from '../../lib/format';
import { CheckIcon, ClockIcon, ShieldIcon } from '../../components/tenant/icons';

const POLL_MS = 3000;
const MAX_POLLS = 20; // ~1 minute — webhooks normally land well inside this

type Phase = 'checking' | 'paid' | 'processing' | 'missing';

/**
 * Paystack sends the payer back here after the hosted checkout. The webhook —
 * not this page — moves funds into escrow, so we just poll the escrow until
 * it reports HeldInEscrow (or time out and let the user check back later).
 */
export default function PaymentCallbackPage() {
  const navigate = useNavigate();
  // Read once, lazily — the pending checkout doesn't change while mounted.
  const [pending] = useState(readPendingCheckout);
  const [phase, setPhase] = useState<Phase>(pending ? 'checking' : 'missing');
  const [amount, setAmount] = useState<number | null>(null);

  useEffect(() => {
    if (!pending) return;
    let polls = 0;
    let timer: ReturnType<typeof setTimeout>;
    let cancelled = false;

    const poll = async () => {
      try {
        const escrow = await getEscrow(pending.escrowId);
        if (cancelled) return;
        setAmount(escrow.amount);
        if (escrow.status === ESCROW_STATUS.heldInEscrow) {
          clearPendingCheckout();
          setPhase('paid');
          return;
        }
      } catch {
        // transient — keep polling until the budget runs out
      }
      if (++polls >= MAX_POLLS) {
        setPhase('processing');
        return;
      }
      timer = setTimeout(poll, POLL_MS);
    };

    void poll();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [pending]);

  if (phase === 'missing') {
    return (
      <Card className="mx-auto max-w-md p-8 text-center">
        <h1 className="text-2xl font-bold text-ink">No payment in progress</h1>
        <p className="mt-2 text-muted">
          We couldn't find a checkout to confirm. If you just paid, your booking will reflect it
          shortly.
        </p>
        <div className="mt-6 flex flex-col gap-2">
          <Button onClick={() => navigate('/bookings')}>View my bookings</Button>
          <Link to="/search" className="text-sm font-semibold text-brand no-underline">
            Keep browsing
          </Link>
        </div>
      </Card>
    );
  }

  if (phase === 'checking') {
    return (
      <Card className="mx-auto max-w-md p-8 text-center">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-brand-50 text-brand">
          <ClockIcon size={26} />
        </div>
        <h1 className="text-2xl font-bold text-ink">Confirming your payment…</h1>
        <p className="mt-2 text-muted">
          Waiting for Paystack to confirm your payment for{' '}
          <span className="font-semibold text-ink">{pending?.propertyTitle}</span>. This usually
          takes a few seconds.
        </p>
      </Card>
    );
  }

  if (phase === 'paid') {
    return (
      <Card className="mx-auto max-w-md p-8 text-center">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-brand-50 text-brand">
          <CheckIcon size={26} />
        </div>
        <h1 className="text-2xl font-bold text-ink">Payment received</h1>
        <p className="mt-2 text-muted">
          {amount != null && <span className="font-semibold text-ink">{formatCedi(amount)} </span>}
          is held safely in escrow for your stay at{' '}
          <span className="font-semibold text-ink">{pending?.propertyTitle}</span>. It's released to
          the host once your stay is confirmed.
        </p>
        <div className="mt-6 flex flex-col gap-2">
          <Button onClick={() => navigate('/bookings')}>View my bookings</Button>
          <Link to="/search" className="text-sm font-semibold text-brand no-underline">
            Keep browsing
          </Link>
        </div>
      </Card>
    );
  }

  return (
    <Card className="mx-auto max-w-md p-8 text-center">
      <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-amber-600">
        <ShieldIcon size={26} />
      </div>
      <h1 className="text-2xl font-bold text-ink">Payment still processing</h1>
      <p className="mt-2 text-muted">
        Paystack hasn't confirmed the payment yet. Don't pay again — your booking will update
        automatically once it clears. Check your bookings in a few minutes.
      </p>
      <div className="mt-6 flex flex-col gap-2">
        <Button onClick={() => navigate('/bookings')}>View my bookings</Button>
      </div>
    </Card>
  );
}
