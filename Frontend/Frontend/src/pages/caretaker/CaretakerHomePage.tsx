import { Link } from 'react-router-dom';
import { getCaretakerDashboard, type CaretakerDashboardDto } from '../../api/roleDashboards';
import { getMyServiceRequests, type ServiceRequestDto } from '../../api/caretakerWorkspace';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import StatCard from '../../components/workspace/StatCard';
import AvailabilityCard from '../../components/caretaker/AvailabilityCard';
import Card from '../../components/ui/Card';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { formatIsoDate } from '../../api/backend';
import { useSession } from '../../store/authStore';
import {
  ToolIcon, ClockIcon, CheckIcon, CardIcon, StarIcon,
} from '../../components/tenant/icons';

const STATUS_TONES: Record<string, BadgeTone> = {
  Pending: 'amber',
  Accepted: 'blue',
  InProgress: 'blue',
  Completed: 'green',
  Cancelled: 'red',
};

interface OverviewData {
  dashboard: CaretakerDashboardDto;
  /** null → list unavailable; the overview degrades to the dashboard stats. */
  requests: ServiceRequestDto[] | null;
}

async function loadOverview(): Promise<OverviewData> {
  const [dashboard, requests] = await Promise.allSettled([
    getCaretakerDashboard(), getMyServiceRequests(),
  ]);
  if (dashboard.status === 'rejected') throw dashboard.reason;
  return {
    dashboard: dashboard.value,
    requests: requests.status === 'fulfilled' ? requests.value : null,
  };
}

function Overview({ data }: { data: OverviewData }) {
  const session = useSession();
  const firstName = (session?.name ?? 'there').split(' ')[0];
  const { dashboard: d, requests } = data;

  const open = requests
    ?.filter((r) => r.status === 'Pending' || r.status === 'Accepted' || r.status === 'InProgress')
    .slice(0, 5) ?? [];

  return (
    <div className="space-y-6">
      <div className="tn-rise">
        <h1 className="text-3xl font-bold tracking-tight text-ink">Welcome back, {firstName}</h1>
        <p className="mt-1 text-muted">Here's the work on your plate.</p>
      </div>

      <AvailabilityCard />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          index={0}
          to="/caretaker/requests"
          icon={<ClockIcon size={18} />}
          label="Pending requests"
          value={d.pendingRequests}
          sub={<span className="text-muted">Waiting for you to accept</span>}
        />
        <StatCard
          index={1}
          to="/caretaker/requests"
          icon={<ToolIcon size={18} />}
          label="Active requests"
          value={d.activeServiceRequests}
          sub={<span className="text-muted">Accepted or in progress</span>}
        />
        <StatCard
          index={2}
          to="/caretaker/requests"
          icon={<CheckIcon size={18} />}
          label="Completed"
          value={d.completedServiceRequests}
          sub={<span className="text-muted">{d.totalServiceRequests} requests all-time</span>}
        />
        <StatCard
          index={3}
          to="/caretaker/requests"
          icon={<CardIcon size={18} />}
          label="Earnings this month"
          value={formatCedi(d.earningsThisMonth)}
          sub={<span className="text-muted">Active assignment compensation</span>}
        />
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
        <section className="min-w-0">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-xl font-bold text-ink">Open requests</h2>
            <Link to="/caretaker/requests" className="text-sm font-semibold text-brand no-underline">View all</Link>
          </div>
          {requests === null ? (
            <Card className="border-dashed p-10 text-center">
              <p className="text-sm text-muted">Service requests are unavailable right now.</p>
            </Card>
          ) : open.length === 0 ? (
            <Card className="border-dashed p-10 text-center">
              <p className="font-semibold text-ink">Nothing waiting on you</p>
              <p className="mt-1 text-sm text-muted">
                New service requests appear here as soon as they're raised against your assignments.
              </p>
            </Card>
          ) : (
            <div className="space-y-3">
              {open.map((r) => (
                <Link key={r.serviceRequestId} to="/caretaker/requests" className="block no-underline">
                  <Card className="tn-lift flex items-center gap-4 p-4">
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
                      <ToolIcon size={18} />
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-semibold text-ink">{r.serviceType}</p>
                      <p className="truncate text-xs text-muted">{r.description} · raised {formatIsoDate(r.createdAt)}</p>
                    </div>
                    <Badge tone={STATUS_TONES[r.status] ?? 'gray'}>{r.status === 'InProgress' ? 'In progress' : r.status}</Badge>
                  </Card>
                </Link>
              ))}
            </div>
          )}
        </section>

        <aside className="min-w-0 space-y-5">
          <Card className="p-5">
            <h3 className="font-bold text-ink">Your rating</h3>
            {d.totalReviews > 0 ? (
              <div className="mt-2 flex items-baseline gap-2">
                <span className="flex items-center gap-1.5 text-3xl font-bold text-ink">
                  <StarIcon size={22} className="text-amber-500" /> {d.averageRating.toFixed(1)}
                </span>
                <span className="text-sm text-muted">from {d.totalReviews} review{d.totalReviews === 1 ? '' : 's'}</span>
              </div>
            ) : (
              <p className="mt-2 text-sm text-muted">
                No reviews yet — ratings appear when requesters review your completed work.
              </p>
            )}
          </Card>

          <Card className="p-5">
            <h3 className="font-bold text-ink">Recent activity</h3>
            <p className="mt-2 text-sm text-muted">{d.recentActivity.message}</p>
          </Card>

          <Card className="border-ink! bg-ink! p-5 text-white">
            <h3 className="font-bold">Respond quickly</h3>
            <p className="mt-1 text-sm text-white/70">
              Accepting requests promptly keeps landlords assigning you more properties.
            </p>
          </Card>
        </aside>
      </div>
    </div>
  );
}

export default function CaretakerHomePage() {
  const state = useAsync(loadOverview, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading your workspace…" errorMessage="Failed to load your dashboard.">
      {(data) => <Overview data={data} />}
    </AsyncBoundary>
  );
}
