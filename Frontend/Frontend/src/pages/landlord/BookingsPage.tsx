import { useMemo, useState } from 'react';
import ClaimsSection from '../../components/landlord/ClaimsSection';
import type { LandlordBooking, LandlordBookingStatus } from '../../types';
import { declineLandlordBooking, getLandlordBookings } from '../../api/landlord';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import Avatar from '../../components/ui/Avatar';
import { formatCedi } from '../../lib/format';
import { CalendarIcon, UserIcon } from '../../components/tenant/icons';

const STATUS: Record<LandlordBookingStatus, { tone: BadgeTone; label: string }> = {
  pending: { tone: 'amber', label: 'Pending' },
  confirmed: { tone: 'green', label: 'Confirmed' },
  'checked-in': { tone: 'blue', label: 'Checked in' },
  completed: { tone: 'gray', label: 'Completed' },
  cancelled: { tone: 'red', label: 'Cancelled' },
};

const TABS: { id: LandlordBookingStatus | 'all'; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'pending', label: 'Pending' },
  { id: 'confirmed', label: 'Confirmed' },
  { id: 'checked-in', label: 'Checked in' },
  { id: 'completed', label: 'Completed' },
];

function BookingsView({ initial }: { initial: LandlordBooking[] }) {
  const [rows, setRows] = useState(initial);
  const [tab, setTab] = useState<LandlordBookingStatus | 'all'>('all');

  // Confirmation/check-in happen server-side when the guest pays and checks in;
  // the landlord's only action here is declining, which cancels with a full refund.
  const decline = async (id: string) => {
    try {
      await declineLandlordBooking(id);
      setRows((rs) => rs.map((b) => (b.id === id ? { ...b, status: 'cancelled' } : b)));
    } catch {
      // leave the row untouched — the cancel didn't go through
    }
  };

  const visible = useMemo(() => (tab === 'all' ? rows : rows.filter((b) => b.status === tab)), [rows, tab]);
  const pending = rows.filter((b) => b.status === 'pending').length;

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Bookings</h1>
      <p className="mt-1 mb-6 text-sm text-muted">{pending} booking{pending === 1 ? '' : 's'} need your approval.</p>

      <div className="mb-6 flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
              tab === t.id ? 'border-brand bg-brand-50 text-brand' : 'border-gray-200 text-gray-600 hover:bg-gray-100'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {visible.length === 0 ? (
        <p className="text-muted">No {tab === 'all' ? '' : STATUS[tab].label.toLowerCase()} bookings.</p>
      ) : (
        <div className="space-y-4">
          {visible.map((b) => (
            <Card key={b.id} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
              <Avatar name={b.guest} size={44} />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-ink">{b.guest}</h3>
                  <Badge tone={STATUS[b.status].tone}>{STATUS[b.status].label}</Badge>
                </div>
                <p className="text-xs text-muted">{b.listing} · {b.id}</p>
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
                  <span className="flex items-center gap-1.5"><CalendarIcon size={14} /> {b.checkIn} → {b.checkOut} · {b.nights}n</span>
                  <span className="flex items-center gap-1.5"><UserIcon size={14} /> {b.guests} guest{b.guests > 1 ? 's' : ''}</span>
                </div>
              </div>
              <div className="flex items-center justify-between gap-3 sm:flex-col sm:items-end">
                <span className="font-bold text-brand">{formatCedi(b.amount)}</span>
                <div className="flex gap-2">
                  {(b.status === 'pending' || b.status === 'confirmed') && (
                    <Button size="sm" variant="ghost" className="text-rose-600 hover:bg-rose-50" onClick={() => decline(b.id)}>Decline</Button>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

export default function LandlordBookingsPage() {
  const state = useAsync(getLandlordBookings, []);
  return (
    <div>
      <AsyncBoundary state={state} loadingMessage="Loading bookings…" errorMessage="Failed to load bookings." emptyMessage="No bookings yet." isEmpty={(r) => r.length === 0}>
        {(rows) => <BookingsView initial={rows} />}
      </AsyncBoundary>

      <ClaimsSection />
    </div>
  );
}
