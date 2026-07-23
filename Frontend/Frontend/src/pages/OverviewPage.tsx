import { getOverview } from '../api/overview';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import StatusBadge from '../components/StatusBadge';
import { formatCurrency } from '../lib/format';

function StatCard({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <Card className="p-5">
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-2 text-2xl font-bold text-ink">{value}</p>
      <p className="mt-1 text-xs text-muted">{hint}</p>
    </Card>
  );
}

export default function OverviewPage() {
  const state = useAsync(getOverview, []);

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">Overview</h1>

      <AsyncBoundary state={state} loadingMessage="Loading overview…" errorMessage="Failed to load overview.">
        {(data) => (
          <div className="space-y-6">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <StatCard
                label="Earnings this month"
                value={formatCurrency(data.monthlyEarnings)}
                hint="Net of management fees"
              />
              <StatCard
                label="Occupancy"
                value={`${data.occupancyRate}%`}
                hint="Booked nights this month"
              />
              <StatCard
                label="Upcoming reservations"
                value={String(data.upcomingCount)}
                hint="Confirmed and pending check-in"
              />
              <StatCard
                label="Avg nightly rate"
                value={formatCurrency(data.avgNightlyRate)}
                hint="Across all reservations"
              />
            </div>

            <Card className="overflow-hidden">
              <div className="border-b border-gray-100 px-6 py-4">
                <h2 className="text-lg font-bold text-ink">Recent reservations</h2>
              </div>
              <ul className="divide-y divide-gray-100">
                {data.recent.map((r) => (
                  <li key={r.id} className="flex items-center justify-between gap-4 px-6 py-4">
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-ink">{r.property}</p>
                      <p className="text-sm text-muted">
                        {r.checkIn} → {r.checkOut}
                      </p>
                    </div>
                    <div className="flex items-center gap-4">
                      <StatusBadge status={r.status} />
                      <span className="font-semibold text-ink">
                        {formatCurrency(r.nightlyRate * r.nights)}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            </Card>
          </div>
        )}
      </AsyncBoundary>
    </div>
  );
}
