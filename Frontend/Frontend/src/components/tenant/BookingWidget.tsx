import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Property } from '../../types';
import Card from '../ui/Card';
import Button from '../ui/Button';
import { formatCedi } from '../../lib/format';
import { quotePrice } from '../../store/bookingStore';
import { isSpanAvailable } from '../../api/calendar';
import { findOverlappingBooking } from '../../api/bookings';
import { useT } from '../../lib/i18n';
import { CheckIcon, ShieldIcon } from './icons';

function todayISO(): string {
  return new Date().toISOString().slice(0, 10);
}

function addDaysISO(iso: string, days: number): string {
  const d = new Date(iso);
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

export interface BookingSelection {
  checkInISO: string;
  checkOutISO: string;
  guests: number;
}

/** Interactive reserve widget: pick dates + guests, see a live quote. */
export default function BookingWidget({ property }: { property: Property }) {
  const t = useT();
  const navigate = useNavigate();
  const defaultIn = todayISO();
  const defaultNights = property.period === 'month' ? 30 : 7;

  const [checkInISO, setCheckInISO] = useState(defaultIn);
  const [checkOutISO, setCheckOutISO] = useState(addDaysISO(defaultIn, defaultNights));
  const [guests, setGuests] = useState(2);
  // Availability of the selected span, from the live calendar (confirmed
  // bookings + landlord blocks). 'unknown' (offline/mock) doesn't block —
  // the server re-checks on booking anyway.
  // Result is keyed by the span it answered for; a span with no answer yet
  // reads as 'checking' (avoids a synchronous reset inside the effect).
  const [availabilityResult, setAvailabilityResult] = useState<{
    key: string; value: 'open' | 'taken' | 'mine' | 'unknown';
  }>({ key: '', value: 'unknown' });

  const quote = useMemo(
    () => quotePrice(property, checkInISO, checkOutISO),
    [property, checkInISO, checkOutISO],
  );

  const valid = quote.nights > 0;
  const minCheckOut = addDaysISO(checkInISO, 1);

  const spanKey = `${property.id}|${checkInISO}|${checkOutISO}`;

  useEffect(() => {
    if (quote.nights <= 0) return;
    let cancelled = false;
    Promise.all([
      isSpanAvailable(property.id, checkInISO, checkOutISO),
      // The server accepts duplicate bookings while payment is pending — flag
      // spans the user has already reserved themselves.
      findOverlappingBooking(property.id, checkInISO, checkOutISO).catch(() => null),
    ])
      .then(([open, ownBooking]) => {
        if (cancelled) return;
        setAvailabilityResult({ key: spanKey, value: ownBooking ? 'mine' : open ? 'open' : 'taken' });
      })
      .catch(() => !cancelled && setAvailabilityResult({ key: spanKey, value: 'unknown' }));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [spanKey, quote.nights]);

  const availability: 'checking' | 'open' | 'taken' | 'mine' | 'unknown' =
    quote.nights > 0 && availabilityResult.key !== spanKey ? 'checking' : availabilityResult.value;

  const reserve = () => {
    if (!valid || availability === 'taken' || availability === 'mine') return;
    const selection: BookingSelection = { checkInISO, checkOutISO, guests };
    navigate(`/checkout/${property.id}`, { state: selection });
  };

  return (
    <Card className="sticky top-20 p-5">
      <p className="text-2xl font-bold text-brand">
        {formatCedi(property.price)}
        <span className="text-sm font-normal text-muted"> / {property.period}</span>
      </p>

      <div className="mt-4 space-y-2">
        <div className="grid grid-cols-2 gap-2">
          <label className="rounded-lg border border-gray-200 px-3 py-2">
            <span className="block text-[11px] text-muted">{t('Check in')}</span>
            <input
              type="date"
              value={checkInISO}
              min={todayISO()}
              onChange={(e) => {
                const next = e.target.value;
                setCheckInISO(next);
                if (next >= checkOutISO) setCheckOutISO(addDaysISO(next, defaultNights));
              }}
              className="w-full bg-transparent text-sm font-medium text-ink outline-none"
            />
          </label>
          <label className="rounded-lg border border-gray-200 px-3 py-2">
            <span className="block text-[11px] text-muted">{t('Check out')}</span>
            <input
              type="date"
              value={checkOutISO}
              min={minCheckOut}
              onChange={(e) => setCheckOutISO(e.target.value)}
              className="w-full bg-transparent text-sm font-medium text-ink outline-none"
            />
          </label>
        </div>

        <div className="flex items-center justify-between rounded-lg border border-gray-200 px-3 py-2">
          <div>
            <p className="text-[11px] text-muted">{t('Guests')}</p>
            <p className="text-sm font-medium text-ink">
              {guests} Guest{guests > 1 ? 's' : ''}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setGuests((g) => Math.max(1, g - 1))}
              className="flex h-7 w-7 items-center justify-center rounded-full border border-gray-300 text-ink disabled:opacity-40"
              disabled={guests <= 1}
              aria-label="Decrease guests"
            >
              −
            </button>
            <button
              type="button"
              onClick={() => setGuests((g) => Math.min(16, g + 1))}
              className="flex h-7 w-7 items-center justify-center rounded-full border border-gray-300 text-ink disabled:opacity-40"
              disabled={guests >= 16}
              aria-label="Increase guests"
            >
              +
            </button>
          </div>
        </div>
      </div>

      {valid && (
        <div className="mt-4 space-y-1.5 text-sm">
          <div className="flex justify-between text-muted">
            <span>
              {formatCedi(quote.perNight)} × {quote.nights} night{quote.nights > 1 ? 's' : ''}
            </span>
            <span className="text-ink">{formatCedi(quote.subtotal)}</span>
          </div>
          <div className="flex justify-between text-muted">
            <span>{t('Service fee')}</span>
            <span className="text-ink">{formatCedi(quote.serviceFee)}</span>
          </div>
          <div className="flex justify-between border-t border-gray-100 pt-1.5 font-semibold text-ink">
            <span>{t('Total')}</span>
            <span>{formatCedi(quote.total)}</span>
          </div>
        </div>
      )}

      <Button
        className="mt-4 w-full"
        onClick={reserve}
        disabled={!valid || availability === 'taken' || availability === 'mine' || availability === 'checking'}
      >
        {availability === 'checking'
          ? t('Checking dates…')
          : property.tag === 'Instant Book'
            ? t('Instant Book')
            : t('Reserve')}
      </Button>
      {!valid && (
        <p className="mt-2 text-center text-xs text-rose-600">
          {t('Check-out must be after check-in.')}
        </p>
      )}
      {valid && availability === 'taken' && (
        <p className="mt-2 text-center text-xs text-rose-600" role="alert">
          {t('Those dates are already booked or blocked — try different ones.')}
        </p>
      )}
      {valid && availability === 'mine' && (
        <p className="mt-2 text-center text-xs text-rose-600" role="alert">
          {t('You already have a booking here for these dates — see My Bookings.')}
        </p>
      )}
      {valid && availability === 'open' && (
        <p className="mt-2 flex items-center justify-center gap-1 text-center text-xs font-medium text-brand">
          <CheckIcon size={12} /> {t('Dates available')}
        </p>
      )}
      <p className="mt-3 flex items-center justify-center gap-1.5 text-xs text-muted">
        <ShieldIcon size={13} className="text-brand" /> {t('Secure payment via Mobile Money')}
      </p>
    </Card>
  );
}
