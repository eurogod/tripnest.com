import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { BookingStatusPill, Pill } from '@/components/badges';
import { Calendar } from '@/components/icons';
import { money, fmtDate } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';

type Raw = Record<string, unknown>;

const str = (o: Raw, ...keys: string[]): string | undefined => {
  for (const k of keys) if (typeof o[k] === 'string') return o[k] as string;
  return undefined;
};
const numOf = (o: Raw, ...keys: string[]): number | undefined => {
  for (const k of keys) if (typeof o[k] === 'number') return o[k] as number;
  return undefined;
};
function reservationsOf(data: Raw | undefined): Raw[] {
  for (const k of ['reservations', 'bookings', 'recentBookings', 'upcomingBookings']) {
    if (Array.isArray(data?.[k])) return data![k] as Raw[];
  }
  return [];
}

export default function HostReservations() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['landlord-dashboard'], queryFn: dashboardApi.landlord, enabled: !!user });
  const props = usePropertyLookup('mine');

  return (
    <div>
      <PageHeader title="Reservations" subtitle="Bookings across all your listings." />
      <Async
        query={query}
        isEmpty={(d) => reservationsOf(d as Raw).length === 0}
        emptyIcon={<Calendar className="h-6 w-6" />}
        emptyTitle="No reservations yet"
        emptySubtitle="When a verified tenant books one of your homes, it’ll appear here."
      >
        {(d) => {
          const rows = reservationsOf(d as Raw);
          return (
            <div className="overflow-x-auto rounded-xl border border-line bg-white">
              <table className="w-full text-sm">
                <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-4 py-3 font-semibold">Property</th>
                    <th className="px-4 py-3 font-semibold">Dates</th>
                    <th className="px-4 py-3 text-right font-semibold">Amount</th>
                    <th className="px-4 py-3 font-semibold">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {rows.map((r, i) => {
                    const pid = str(r, 'propertyId');
                    const title = str(r, 'propertyTitle', 'title') ?? (pid ? props.get(pid)?.title : undefined) ?? 'Property';
                    const status = numOf(r, 'status');
                    const statusStr = str(r, 'status');
                    return (
                      <tr key={str(r, 'bookingId', 'id') ?? i}>
                        <td className="px-4 py-3 font-medium">{title}</td>
                        <td className="px-4 py-3 text-muted">
                          {fmtDate(str(r, 'checkInDate'))} → {fmtDate(str(r, 'checkOutDate'))}
                        </td>
                        <td className="px-4 py-3 text-right font-bold">{money(numOf(r, 'totalAmount', 'amount'))}</td>
                        <td className="px-4 py-3">
                          {status != null ? <BookingStatusPill status={status} /> : statusStr ? <Pill>{statusStr}</Pill> : '—'}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          );
        }}
      </Async>
    </div>
  );
}
