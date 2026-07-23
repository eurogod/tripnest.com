import { useState } from 'react';
import type { Reservation } from '../types';
import { formatCurrency } from '../lib/format';

interface ReservationDetailProps {
  reservation: Reservation;
  onClose: () => void;
}

type Tab = 'details' | 'reviews';

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between py-2.5">
      <span className="text-muted">{label}</span>
      <span className="font-medium text-ink">{value}</span>
    </div>
  );
}

//  Slide-over panel showing a reservation's trip details and payo1ut breakdown.
export default function ReservationDetail({ reservation, onClose }: ReservationDetailProps) {
  const [tab, setTab] = useState<Tab>('details');

  const netRevenue = reservation.nightlyRate * reservation.nights;
  const managementFee = (netRevenue * reservation.managementFeePercent) / 100;
  const ownerPayout = netRevenue - managementFee;

  return (
    <>
      <div
        className="fixed inset-0 z-40 bg-black/20"
        onClick={onClose}
        aria-hidden
      />
      <aside className="fixed inset-y-0 right-0 z-50 m-5 rounded-2xl flex w-full max-w-[440px] flex-col bg-white shadow-2xl">
        <header className="flex items-center justify-between px-8 pt-8 pb-4">
          <h2 className="text-2xl font-bold text-ink">Reservation Details</h2>
          <button
            onClick={onClose}
            aria-label="Close"
            className="flex h-8 w-8 items-center justify-center rounded-full text-xl text-muted hover:bg-gray-100"
          >
            ×
          </button>
        </header>

        <div className="mx-8 grid grid-cols-2 gap-1 rounded-xl bg-gray-100 p-1">
          <button
            onClick={() => setTab('details')}
            className={`rounded-lg py-2 text-sm font-semibold transition-colors ${
              tab === 'details' ? 'bg-white text-ink shadow-sm' : 'text-muted'
            }`}
          >
            Trip details
          </button>
          <button
            onClick={() => setTab('reviews')}
            className={`rounded-lg py-2 text-sm font-semibold transition-colors ${
              tab === 'reviews' ? 'bg-white text-ink shadow-sm' : 'text-muted'
            }`}
          >
            Guest reviews
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-8 py-6">
          {tab === 'details' ? (
            <div className="divide-y divide-gray-100">
              <Row label="Check-in" value={reservation.checkInFull} />
              <Row label="Check-out" value={reservation.checkOutFull} />
              <Row label="Guests" value={reservation.guests} />
              <Row label="Nights" value={reservation.nights} />
              <Row label="Trip type" value={reservation.tripType} />
              <Row label="Booked through" value={reservation.bookedThrough} />
              <Row
                label="Status"
                value={
                  <span className="font-semibold text-brand">✓ Confirmed</span>
                }
              />
            </div>
          ) : reservation.reviews.length > 0 ? (
            <ul className="space-y-4">
              {reservation.reviews.map((review, i) => (
                <li key={i} className="rounded-xl border border-gray-100 p-4">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-ink">{review.name}</span>
                    <span className="text-sm text-muted">{review.date}</span>
                  </div>
                  <p className="mt-1 text-amber-500">{'★'.repeat(review.stars)}</p>
                  <p className="mt-2 text-muted">{review.text}</p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted">No guest reviews yet.</p>
          )}
        </div>

        <footer className="border-t border-gray-100 px-8 py-6">
          <Row label="Nightly rate" value={formatCurrency(reservation.nightlyRate)} />
          <Row
            label={`Net revenue (${reservation.nights} Nights)`}
            value={formatCurrency(netRevenue)}
          />
          <Row label="Management fee" value={formatCurrency(managementFee)} />
          <div className="mt-2 flex items-center justify-between border-t border-gray-200 pt-4">
            <span className="font-semibold text-ink">Owner payout</span>
            <span className="text-lg font-bold text-ink">
              {formatCurrency(ownerPayout)}
            </span>
          </div>
        </footer>
      </aside>
    </>
  );
}
