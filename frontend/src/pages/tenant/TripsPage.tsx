import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bookingsApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button, Spinner } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { BookingStatusPill } from '@/components/badges';
import { Calendar, MapPin } from '@/components/icons';
import { money, fmtDate, nights, propertyPhoto } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { BookingStatus } from '@/lib/enums';
import type { Booking } from '@/types/api';

export default function TripsPage() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['my-bookings'], queryFn: bookingsApi.mine, enabled: !!user });
  const props = usePropertyLookup('all');
  const [cancelling, setCancelling] = useState<Booking | null>(null);

  return (
    <div>
      <PageHeader title="My trips" subtitle="Every booking, past and upcoming." />
      <Async
        query={query}
        emptyIcon={<Calendar className="h-6 w-6" />}
        emptyTitle="No trips yet"
        emptySubtitle="When you book a verified stay it’ll show up here."
        emptyAction={
          <Link to="/search">
            <Button>Browse stays</Button>
          </Link>
        }
      >
        {(bookings) => (
          <div className="space-y-4">
            {bookings.map((b) => {
              const p = props.get(b.propertyId);
              const canCancel = b.status === BookingStatus.Pending || b.status === BookingStatus.Confirmed;
              return (
                <div key={b.bookingId} className="card flex flex-col gap-4 p-4 sm:flex-row">
                  <Link to={`/property/${b.propertyId}`} className="h-32 w-full shrink-0 overflow-hidden rounded-lg bg-line sm:w-44">
                    {p && <img src={propertyPhoto(p)} alt={p.title} className="h-full w-full object-cover" />}
                  </Link>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-start justify-between gap-2">
                      <div>
                        <h3 className="font-bold">{p?.title ?? 'Property'}</h3>
                        {p && (
                          <p className="mt-0.5 flex items-center gap-1 text-sm text-muted">
                            <MapPin className="h-3.5 w-3.5" /> {p.location}
                          </p>
                        )}
                      </div>
                      <BookingStatusPill status={b.status} />
                    </div>
                    <p className="mt-2 text-sm text-muted">
                      {fmtDate(b.checkInDate)} → {fmtDate(b.checkOutDate)} · {nights(b.checkInDate, b.checkOutDate)} night(s)
                    </p>
                    <div className="mt-3 flex items-center justify-between">
                      <span className="font-bold">{money(b.totalAmount)}</span>
                      {canCancel && (
                        <Button variant="outline" size="sm" onClick={() => setCancelling(b)}>
                          Cancel
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Async>

      <CancelModal booking={cancelling} onClose={() => setCancelling(null)} />
    </div>
  );
}

function CancelModal({ booking, onClose }: { booking: Booking | null; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  const preview = useQuery({
    queryKey: ['cancel-preview', booking?.bookingId],
    queryFn: () => bookingsApi.cancellationPreview(booking!.bookingId),
    enabled: !!booking,
  });

  async function confirm() {
    if (!booking) return;
    setBusy(true);
    try {
      await bookingsApi.cancel(booking.bookingId);
      toast.success('Booking cancelled');
      qc.invalidateQueries({ queryKey: ['my-bookings'] });
      onClose();
    } catch {
      toast.error('Could not cancel this booking');
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={!!booking} onClose={onClose} title="Cancel booking" maxWidth="max-w-md">
      <p className="text-sm text-muted">Review your refund before confirming. This can’t be undone.</p>
      <div className="my-4 rounded-xl bg-surface p-4">
        {preview.isLoading ? (
          <div className="flex justify-center py-2 text-brand-600">
            <Spinner className="h-5 w-5" />
          </div>
        ) : preview.data ? (
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted">Refund ({preview.data.refundPercentage}%)</span>
            <span className="text-lg font-extrabold text-success">{money(preview.data.refundAmount)}</span>
          </div>
        ) : (
          <p className="text-sm text-muted">Refund details unavailable.</p>
        )}
      </div>
      <div className="flex gap-3">
        <Button variant="ghost" block onClick={onClose}>
          Keep booking
        </Button>
        <Button variant="danger" block loading={busy} onClick={confirm}>
          Confirm cancel
        </Button>
      </div>
    </Modal>
  );
}
