import { useEffect, useState } from 'react';
import { getMyRentInvoices, payRentInvoice, verifyRentInvoice, RENT_STATUS, type RentInvoiceDto } from '../../api/rent';
import { getBookingShares, payShare, verifyShare, SHARE_STATUS, type BookingShareDto } from '../../api/bookings';
import { apiGetList } from '../../api/client';
import type { BookingResponseDto } from '../../api/backend';
import { getSession } from '../../store/authStore';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge, { type BadgeTone } from '../ui/Badge';
import { formatCedi } from '../../lib/format';

const RENT_TONE: Record<string, BadgeTone> = {
  Upcoming: 'gray', Due: 'amber', Paid: 'green', Overdue: 'red', Cancelled: 'gray',
};

/**
 * Money the tenant still owes outside the upfront booking charge: monthly rent invoices
 * (long-term stays) and their share of group bookings. Paying opens the gateway checkout;
 * with the dev-simulated gateway there's no redirect, so we verify immediately instead.
 */
export default function RentAndSharesSection() {
  const [invoices, setInvoices] = useState<RentInvoiceDto[]>([]);
  const [shares, setShares] = useState<BookingShareDto[]>([]);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);

  const load = async () => {
    const me = getSession()?.userId;
    const [inv, bookings] = await Promise.all([
      getMyRentInvoices().catch(() => [] as RentInvoiceDto[]),
      apiGetList<BookingResponseDto>('/api/bookings/user/my-bookings').catch(() => [] as BookingResponseDto[]),
    ]);
    const perBooking = await Promise.all(
      bookings.map((b) => getBookingShares(b.bookingId).catch(() => [] as BookingShareDto[])),
    );
    setInvoices(inv);
    // Only MY unpaid shares are actionable here.
    setShares(perBooking.flat().filter((sh) => sh.participantUserId === me && sh.status === 0));
  };

  // Deferred a tick: the linter can't see that load() only sets state after awaits.
  useEffect(() => { void Promise.resolve().then(load); }, []);

  const settle = async (
    id: string,
    pay: () => Promise<{ checkoutUrl?: string | null }>,
    verify: () => Promise<unknown>,
  ) => {
    setBusyId(id);
    setNote(null);
    try {
      const res = await pay();
      if (res.checkoutUrl) {
        window.location.assign(res.checkoutUrl); // gateway checkout; callback lands on /payment/callback
        return;
      }
      await verify(); // simulated gateway: no redirect — confirm directly
      await load();
      setNote('Payment confirmed.');
    } catch (e) {
      setNote(e instanceof Error ? e.message : 'Payment could not be started.');
    } finally {
      setBusyId(null);
    }
  };

  const payableInvoices = invoices.filter((i) => RENT_STATUS[i.status] === 'Due' || RENT_STATUS[i.status] === 'Overdue');
  const otherInvoices = invoices.filter((i) => !payableInvoices.includes(i));

  if (invoices.length === 0 && shares.length === 0) return null;

  return (
    <section className="mt-8 space-y-4">
      {invoices.length > 0 && (
        <Card className="p-5">
          <h2 className="text-base font-bold text-ink">Monthly rent</h2>
          <div className="mt-3 space-y-2">
            {[...payableInvoices, ...otherInvoices].map((inv) => {
              const st = RENT_STATUS[inv.status] ?? 'Upcoming';
              return (
                <div key={inv.invoiceId} className="flex items-center justify-between gap-3 rounded-lg bg-gray-50 px-3 py-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-ink">
                      {new Date(inv.periodStart).toLocaleDateString()} – {new Date(inv.periodEnd).toLocaleDateString()}
                    </p>
                    <p className="text-xs text-muted">Due {new Date(inv.dueDate).toLocaleDateString()}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <span className="text-sm font-semibold text-ink">{formatCedi(inv.amount)}</span>
                    <Badge tone={RENT_TONE[st]}>{st}</Badge>
                    {(st === 'Due' || st === 'Overdue') && (
                      <Button size="sm" disabled={busyId === inv.invoiceId}
                        onClick={() => { void settle(inv.invoiceId, () => payRentInvoice(inv.invoiceId), () => verifyRentInvoice(inv.invoiceId)); }}>
                        {busyId === inv.invoiceId ? 'Starting…' : 'Pay'}
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      )}

      {shares.length > 0 && (
        <Card className="p-5">
          <h2 className="text-base font-bold text-ink">Your share of group bookings</h2>
          <p className="mt-1 text-xs text-muted">
            The booking is only confirmed once every member has paid their share.
          </p>
          <div className="mt-3 space-y-2">
            {shares.map((sh) => (
              <div key={sh.shareId} className="flex items-center justify-between gap-3 rounded-lg bg-gray-50 px-3 py-2">
                <p className="text-sm text-ink">Booking {sh.bookingId.slice(0, 8)}…</p>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-sm font-semibold text-ink">{formatCedi(sh.amount)}</span>
                  <Badge tone="amber">{SHARE_STATUS[sh.status]}</Badge>
                  <Button size="sm" disabled={busyId === sh.shareId}
                    onClick={() => { void settle(sh.shareId, () => payShare(sh.shareId), () => verifyShare(sh.shareId)); }}>
                    {busyId === sh.shareId ? 'Starting…' : 'Pay my share'}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      {note && <p className="text-sm text-muted">{note}</p>}
    </section>
  );
}
