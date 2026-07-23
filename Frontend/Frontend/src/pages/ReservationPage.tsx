import { useState } from 'react';
import type { Reservation, ReservationStatus } from '../types';
import { getReservationById, getReservations } from '../api/reservations';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import StatusBadge from '../components/StatusBadge';
import ReservationDetail from '../components/ReservationDetail';

const STATUS_RANK: Record<ReservationStatus, number> = {
  upcoming: 0,
  complete: 1,
  canceled: 2,
};

export default function ReservationPage() {
  const state = useAsync(getReservations, []);
  const [selected, setSelected] = useState<Reservation | null>(null);
  const [sortAsc, setSortAsc] = useState(true);

  // Show the row right away; the details call fills in the earnings breakdown
  // and guest reviews when it lands (unless another row was picked meanwhile).
  const select = (row: Reservation) => {
    setSelected(row);
    getReservationById(row.id)
      .then((full) => {
        if (full) setSelected((cur) => (cur?.id === row.id ? full : cur));
      })
      .catch(() => {});
  };

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">Reservations</h1>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading reservations…"
        errorMessage="Failed to load reservations."
        emptyMessage="No reservations yet."
        isEmpty={(rows) => rows.length === 0}
      >
        {(data) => {
          const rows = [...data].sort((a, b) => {
            const diff = STATUS_RANK[a.status] - STATUS_RANK[b.status];
            return sortAsc ? diff : -diff;
          });

          return (
            <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] border-separate border-spacing-y-3">
              <thead>
                <tr className="text-left text-xs font-semibold uppercase tracking-wide text-muted">
                  <th className="px-5 pb-1 font-semibold">Property</th>
                  <th className="px-5 pb-1 font-semibold">
                    <button
                      onClick={() => setSortAsc((s) => !s)}
                      className="inline-flex items-center gap-1 uppercase"
                    >
                      Status <span className="text-[10px]">{sortAsc ? '↑' : '↓'}</span>
                    </button>
                  </th>
                  <th className="px-5 pb-1 font-semibold">Check-in</th>
                  <th className="px-5 pb-1 font-semibold">Check-out</th>
                  <th className="px-5 pb-1 font-semibold">Nights</th>
                  <th className="px-5 pb-1 font-semibold">Guests</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => {
                  const isSelected = selected?.id === r.id;
                  return (
                    <tr
                      key={r.id}
                      onClick={() => select(r)}
                      className={`cursor-pointer bg-white shadow-[0_1px_2px_rgba(0,0,0,0.04)] [&>td]:border-y [&>td]:border-gray-100 [&>td:first-child]:rounded-l-xl [&>td:first-child]:border-l [&>td:last-child]:rounded-r-xl [&>td:last-child]:border-r ${
                        isSelected
                          ? '[&>td]:border-brand! [&>td]:bg-brand-50'
                          : 'hover:[&>td]:bg-gray-50'
                      }`}
                    >
                      <td className="px-5 py-4 font-semibold text-ink">{r.property}</td>
                      <td className="px-5 py-4">
                        <StatusBadge status={r.status} />
                      </td>
                      <td className="px-5 py-4 text-ink">{r.checkIn}</td>
                      <td className="px-5 py-4 text-ink">{r.checkOut}</td>
                      <td className="px-5 py-4 text-ink">{r.nights}</td>
                      <td className="px-5 py-4 text-ink">{r.guests}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            </div>
          );
        }}
      </AsyncBoundary>

      {selected && (
        <ReservationDetail
          reservation={selected}
          onClose={() => setSelected(null)}
        />
      )}
    </div>
  );
}
