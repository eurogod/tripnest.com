import { useState } from 'react';
import type { Trip, TripStatus } from '../types';
import { cancelTrip, getCancellationPreview, getTrips } from '../api/trips';
import { submitReview } from '../api/reviews';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import Badge, { type BadgeTone } from '../components/ui/Badge';
import { formatCurrency } from '../lib/format';

const STATUS_TONE: Record<TripStatus, BadgeTone> = {
  upcoming: 'blue',
  completed: 'green',
  canceled: 'red',
};

const TABS: { id: TripStatus; label: string }[] = [
  { id: 'upcoming', label: 'Upcoming' },
  { id: 'completed', label: 'Completed' },
  { id: 'canceled', label: 'Canceled' },
];

/** Star picker for the review form (1–5, filled up to the current choice). */
function Stars({ value, onChange }: { value: number; onChange: (n: number) => void }) {
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Rating">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          aria-label={`${n} star${n > 1 ? 's' : ''}`}
          onClick={() => onChange(n)}
          className={`text-2xl leading-none ${n <= value ? 'text-amber-400' : 'text-gray-300'}`}
        >
          ★
        </button>
      ))}
    </div>
  );
}

function TripCard({ trip }: { trip: Trip }) {
  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState(trip.status);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState('');
  const [reviewed, setReviewed] = useState(false);

  const cancel = async () => {
    setBusy(true);
    setNote(null);
    try {
      // Show the money consequence BEFORE asking for confirmation — the backend computes the
      // exact refund from the property's cancellation policy, no client-side guessing.
      const p = await getCancellationPreview(trip.id);
      const ok = window.confirm(
        `Cancel this trip?\n\nPolicy: ${p.policyName}\nRefund: GH₵ ${p.refundAmount.toLocaleString('en-GH')} (${p.refundPercentage}% — ${Math.floor(p.daysUntilCheckIn)} days before check-in)`,
      );
      if (ok) {
        await cancelTrip(trip.id);
        setStatus('canceled');
        setNote('Trip cancelled. Any refund follows the policy shown.');
      }
    } catch (e) {
      setNote(e instanceof Error ? e.message : 'Could not cancel this trip.');
    } finally {
      setBusy(false);
    }
  };

  const sendReview = async () => {
    setBusy(true);
    setNote(null);
    try {
      await submitReview({ bookingId: trip.id, propertyId: trip.propertyId, rating, comment: comment.trim() || undefined });
      setReviewed(true);
      setReviewOpen(false);
      setNote('Thanks — your review is in.');
    } catch (e) {
      setNote(e instanceof Error ? e.message : 'Could not submit the review.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="flex overflow-hidden">
      <div className="w-28 shrink-0" style={{ backgroundColor: trip.coverColor }} />
      <div className="flex-1 p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-semibold text-ink">{trip.destination}</p>
            <p className="truncate text-sm text-muted">{trip.property}</p>
          </div>
          <Badge tone={STATUS_TONE[status]}>{status}</Badge>
        </div>

        <p className="mt-3 text-sm text-muted">
          {trip.checkIn} → {trip.checkOut} · {trip.nights} nights · {trip.guests} guests
        </p>

        {open && (
          <dl className="mt-3 grid grid-cols-2 gap-2 rounded-lg bg-gray-50 p-3 text-sm">
            <dt className="text-muted">Reference</dt>
            <dd className="text-right font-medium text-ink">{trip.id}</dd>
            <dt className="text-muted">Nightly rate</dt>
            <dd className="text-right font-medium text-ink">{formatCurrency(Math.round(trip.price / trip.nights))}</dd>
            <dt className="text-muted">Total</dt>
            <dd className="text-right font-medium text-ink">{formatCurrency(trip.price)}</dd>
          </dl>
        )}

        {open && status === 'upcoming' && (
          <div className="mt-3">
            <Button variant="ghost" size="sm" disabled={busy} onClick={() => { void cancel(); }}>
              {busy ? 'Checking refund…' : 'Cancel trip'}
            </Button>
          </div>
        )}

        {open && status === 'completed' && !reviewed && (
          <div className="mt-3">
            {reviewOpen ? (
              <div className="space-y-2 rounded-lg bg-gray-50 p-3">
                <Stars value={rating} onChange={setRating} />
                <textarea
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  placeholder="How was your stay? (optional)"
                  rows={3}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
                />
                <div className="flex gap-2">
                  <Button size="sm" disabled={busy} onClick={() => { void sendReview(); }}>
                    {busy ? 'Sending…' : 'Submit review'}
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => setReviewOpen(false)}>Cancel</Button>
                </div>
              </div>
            ) : (
              <Button variant="ghost" size="sm" onClick={() => setReviewOpen(true)}>Leave a review</Button>
            )}
          </div>
        )}

        {note && <p className="mt-2 text-sm text-muted">{note}</p>}

        <div className="mt-4 flex items-center justify-between">
          <span className="font-semibold text-ink">{formatCurrency(trip.price)}</span>
          <Button variant="ghost" size="sm" onClick={() => setOpen((o) => !o)}>
            {open ? 'Hide details' : trip.status === 'upcoming' ? 'Manage' : 'View details'}
          </Button>
        </div>
      </div>
    </Card>
  );
}

export default function MyTripsPage() {
  const state = useAsync(getTrips, []);
  const [tab, setTab] = useState<TripStatus>('upcoming');

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">My Trips</h1>

      <div className="mb-5 flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`rounded-full px-4 py-1.5 text-sm font-semibold transition-colors ${
              tab === t.id
                ? 'bg-ink text-white'
                : 'bg-gray-100 text-muted hover:bg-gray-200'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading trips…"
        errorMessage="Failed to load trips."
      >
        {(rows) => {
          const visible = rows.filter((t) => t.status === tab);
          return visible.length === 0 ? (
            <p className="text-muted">No {tab} trips.</p>
          ) : (
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              {visible.map((t) => (
                <TripCard key={t.id} trip={t} />
              ))}
            </div>
          );
        }}
      </AsyncBoundary>
    </div>
  );
}
