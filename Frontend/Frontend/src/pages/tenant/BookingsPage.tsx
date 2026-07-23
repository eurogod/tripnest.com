import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { BookingStatus } from '../../types';
import { getBookings } from '../../api/bookings';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { useT } from '../../lib/i18n';
import { CalendarIcon, MapPinIcon, UserIcon } from '../../components/tenant/icons';

const TABS: { id: BookingStatus | 'all'; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'upcoming', label: 'Upcoming' },
  { id: 'active', label: 'Active' },
  { id: 'past', label: 'Past' },
  { id: 'cancelled', label: 'Cancelled' },
];

const STATUS: Record<BookingStatus, { tone: BadgeTone; label: string }> = {
  upcoming: { tone: 'green', label: 'Upcoming' },
  active: { tone: 'blue', label: 'Active' },
  past: { tone: 'gray', label: 'Completed' },
  cancelled: { tone: 'red', label: 'Cancelled' },
};

export default function BookingsPage() {
  const state = useAsync(getBookings, []);
  const [tab, setTab] = useState<BookingStatus | 'all'>('all');
  const t = useT();

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold text-ink">{t('Bookings')}</h1>

      <div className="mb-6 flex flex-wrap gap-2">
        {TABS.map((tabDef) => (
          <button
            key={tabDef.id}
            onClick={() => setTab(tabDef.id)}
            className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
              tab === tabDef.id
                ? 'border-brand bg-brand-50 text-brand'
                : 'border-gray-200 text-gray-600 hover:bg-gray-100'
            }`}
          >
            {t(tabDef.label)}
          </button>
        ))}
      </div>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading bookings…"
        errorMessage="Failed to load bookings."
      >
        {(all) => {
          const rows = tab === 'all' ? all : all.filter((b) => b.status === tab);
          return rows.length === 0 ? (
            <p className="text-muted">{t('No bookings yet.')}</p>
          ) : (
            <div className="space-y-4">
              {rows.map((b) => (
                <Card key={b.id} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
                  <div className="h-24 w-full shrink-0 rounded-lg bg-gradient-to-br from-brand-50 to-gray-200 sm:w-36" />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-semibold text-ink">{b.property}</h3>
                      <Badge tone={STATUS[b.status].tone}>{t(STATUS[b.status].label)}</Badge>
                    </div>
                    <p className="text-xs text-muted">TN-ID: {b.propertyId} · {b.id}</p>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
                      <span className="flex items-center gap-1.5"><MapPinIcon size={14} /> {b.location}</span>
                      <span className="flex items-center gap-1.5"><CalendarIcon size={14} /> {b.checkIn} → {b.checkOut}</span>
                      <span className="flex items-center gap-1.5"><UserIcon size={14} /> {b.guests} guest{b.guests > 1 ? 's' : ''}</span>
                    </div>
                  </div>
                  <div className="flex items-center justify-between gap-4 sm:flex-col sm:items-end">
                    <span className="font-bold text-brand">
                      {formatCedi(b.amount)}
                      <span className="text-xs font-normal text-muted"> / {b.period}</span>
                    </span>
                    <Link to={`/property/${b.propertyId}`}>
                      <Button size="sm" variant="ghost">{t('View property')}</Button>
                    </Link>
                  </div>
                </Card>
              ))}
            </div>
          );
        }}
      </AsyncBoundary>
    </div>
  );
}
