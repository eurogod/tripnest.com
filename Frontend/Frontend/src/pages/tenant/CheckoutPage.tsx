import { useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import type { Property } from '../../types';
import { getPropertyById } from '../../api/properties';
import { createBooking, findOverlappingBooking } from '../../api/bookings';
import { initiateEscrow, isSimulatedCheckout, savePendingCheckout, verifyEscrowPayment } from '../../api/escrow';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import { formatCedi, formatDateShort } from '../../lib/format';
import { quotePrice } from '../../store/bookingStore';
import type { BookingSelection } from '../../components/tenant/BookingWidget';
import { CalendarIcon, CardIcon, MapPinIcon, ShieldIcon, UserIcon } from '../../components/tenant/icons';

type PayPhase = 'idle' | 'booking' | 'initiating';

function defaultSelection(property: Property): BookingSelection {
  const today = new Date();
  const out = new Date(today);
  out.setDate(out.getDate() + (property.period === 'month' ? 30 : 7));
  return {
    checkInISO: today.toISOString().slice(0, 10),
    checkOutISO: out.toISOString().slice(0, 10),
    guests: 2,
  };
}

function Review({ property }: { property: Property }) {
  const location = useLocation();
  const navigate = useNavigate();
  const selection = (location.state as BookingSelection | null) ?? defaultSelection(property);
  const quote = quotePrice(property, selection.checkInISO, selection.checkOutISO);

  const [phase, setPhase] = useState<PayPhase>('idle');
  const [error, setError] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState<{ bookingId: string; amount: number; held: boolean } | null>(null);

  const submitting = phase !== 'idle';
  const canPay = quote.nights > 0 && !submitting;

  // Book first (the server checks availability and derives the price), then
  // open an escrow for it — Paystack's hosted page takes the actual payment.
  const confirm = async () => {
    setError(null);
    try {
      setPhase('booking');
      // The server accepts duplicate bookings while payment is pending — stop
      // a repeat reservation of dates the user already holds.
      const existing = await findOverlappingBooking(
        property.id, selection.checkInISO, selection.checkOutISO,
      ).catch(() => null);
      if (existing) {
        setError(
          `You already have a booking here from ${formatDateShort(existing.checkIn)} to ${formatDateShort(existing.checkOut)} — see My Bookings.`,
        );
        return;
      }
      const booking = await createBooking({
        propertyId: property.id,
        checkInDate: selection.checkInISO,
        checkOutDate: selection.checkOutISO,
      });

      setPhase('initiating');
      const escrow = await initiateEscrow(booking.id);

      if (escrow.checkoutUrl && !isSimulatedCheckout(escrow.checkoutUrl)) {
        savePendingCheckout({
          escrowId: escrow.escrowId,
          bookingId: booking.id,
          propertyTitle: property.title,
        });
        window.location.assign(escrow.checkoutUrl);
        return; // navigating away — Paystack redirects back to /payment/callback
      }

      // Dev environment without Paystack keys (simulated gateway): there's no hosted
      // page to redirect to, so complete the flow here — verify holds the escrow and
      // confirms the booking (the backend's simulated gateway reports success).
      try {
        const held = await verifyEscrowPayment(booking.id);
        setConfirmed({ bookingId: booking.id, amount: held.amount, held: true });
      } catch {
        // Verification didn't hold (e.g. real gateway needs the redirect) — show as pending.
        setConfirmed({ bookingId: booking.id, amount: escrow.amount, held: false });
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Booking failed. Please try again.');
    } finally {
      setPhase('idle');
    }
  };

  const payLabel =
    phase === 'booking'
      ? 'Reserving your dates…'
      : phase === 'initiating'
        ? 'Preparing secure payment…'
        : `Confirm & pay ${formatCedi(quote.total)}`;

  if (confirmed) {
    return (
      <Card className="mx-auto max-w-md p-8 text-center">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-brand-50 text-brand">
          <ShieldIcon size={26} />
        </div>
        <h1 className="text-2xl font-bold text-ink">
          {confirmed.held ? 'Booking confirmed' : 'Booking reserved'}
        </h1>
        <p className="mt-2 text-muted">
          Your reservation at <span className="font-semibold text-ink">{property.title}</span> is in.
          Reference <span className="font-semibold text-ink">{confirmed.bookingId.slice(0, 8).toUpperCase()}</span>
          {' '}· {formatCedi(confirmed.amount)}{' '}
          {confirmed.held
            ? 'held in escrow. Sign your rental agreement to finalise your stay.'
            : 'held pending payment.'}
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
    <div className="mx-auto max-w-3xl">
      <Link to={`/property/${property.id}`} className="text-sm font-semibold text-brand no-underline">
        ← Back to listing
      </Link>
      <h1 className="mt-4 mb-6 text-3xl font-bold text-ink">Review &amp; pay</h1>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_340px]">
        <div className="space-y-6">
          <Card className="p-5">
            <h2 className="mb-3 text-lg font-bold text-ink">Your trip</h2>
            <div className="space-y-2 text-sm text-ink">
              <p className="flex items-center gap-2">
                <CalendarIcon size={15} className="text-brand" />
                {formatDateShort(selection.checkInISO)} → {formatDateShort(selection.checkOutISO)}
                <span className="text-muted">
                  · {quote.nights} night{quote.nights > 1 ? 's' : ''}
                </span>
              </p>
              <p className="flex items-center gap-2">
                <UserIcon size={15} className="text-brand" />
                {selection.guests} guest{selection.guests > 1 ? 's' : ''}
              </p>
            </div>
          </Card>

          <Card className="p-5">
            <h2 className="mb-3 text-lg font-bold text-ink">Pay with Paystack</h2>
            <div className="flex items-start gap-3 rounded-lg border border-gray-100 bg-gray-50 p-4">
              <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
                <CardIcon size={16} />
              </span>
              <div className="text-sm text-ink">
                <p className="font-semibold">Secure hosted checkout</p>
                <p className="mt-1 text-muted">
                  You'll be redirected to Paystack to pay with Mobile Money (MTN, Telecel,
                  AirtelTigo) or a Visa/Mastercard, then brought back here. Your money is held in
                  escrow until your stay is confirmed.
                </p>
              </div>
            </div>
          </Card>
        </div>

        <aside>
          <Card className="sticky top-20 p-5">
            <div className="flex gap-3">
              <div className="h-16 w-20 shrink-0 rounded-lg bg-gradient-to-br from-brand-50 to-gray-200" />
              <div className="min-w-0">
                <p className="truncate font-semibold text-ink">{property.title}</p>
                <p className="flex items-center gap-1 text-xs text-muted">
                  <MapPinIcon size={12} /> {property.location}
                </p>
                {property.verified && (
                  <Badge tone="green" className="mt-1">
                    Verified
                  </Badge>
                )}
              </div>
            </div>

            <div className="mt-4 space-y-1.5 border-t border-gray-100 pt-4 text-sm">
              <div className="flex justify-between text-muted">
                <span>
                  {formatCedi(quote.perNight)} × {quote.nights} night{quote.nights > 1 ? 's' : ''}
                </span>
                <span className="text-ink">{formatCedi(quote.subtotal)}</span>
              </div>
              <div className="flex justify-between text-muted">
                <span>Service fee</span>
                <span className="text-ink">{formatCedi(quote.serviceFee)}</span>
              </div>
              <div className="flex justify-between border-t border-gray-100 pt-2 text-base font-bold text-ink">
                <span>Total</span>
                <span>{formatCedi(quote.total)}</span>
              </div>
              <p className="pt-1 text-xs text-muted">
                Final amount is confirmed by TripNest when your dates are reserved.
              </p>
            </div>

            <Button className="mt-4 w-full" onClick={confirm} disabled={!canPay}>
              {payLabel}
            </Button>
            {error && (
              <p className="mt-2 text-center text-xs text-rose-600" role="alert">
                {error}
              </p>
            )}
            <p className="mt-3 flex items-center justify-center gap-1.5 text-xs text-muted">
              <ShieldIcon size={13} className="text-brand" /> Escrow-protected via Paystack
            </p>
          </Card>
        </aside>
      </div>
    </div>
  );
}

export default function CheckoutPage() {
  const { id = '' } = useParams();
  const state = useAsync(() => getPropertyById(id), [id]);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading checkout…"
      errorMessage="Failed to load checkout."
    >
      {(property) =>
        property ? (
          <Review property={property} />
        ) : (
          <div>
            <p className="text-muted">Property not found.</p>
            <Link to="/search" className="text-sm font-semibold text-brand no-underline">
              ← Back to search
            </Link>
          </div>
        )
      }
    </AsyncBoundary>
  );
}
