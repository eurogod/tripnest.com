import { Link } from 'react-router-dom';
import { getAgentDashboard, type AgentDashboardDto } from '../../api/roleDashboards';
import { getMyViewingRequests, type ViewingRequestDto } from '../../api/agentWorkspace';
import { getPendingWalkthroughs } from '../../api/walkthroughs';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import StatCard from '../../components/workspace/StatCard';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import { formatIsoDateFull } from '../../api/backend';
import { useSession } from '../../store/authStore';
import {
  FileIcon, CalendarIcon, KeyIcon, ShieldIcon, UserIcon, ChevronRightIcon,
} from '../../components/tenant/icons';

interface OverviewData {
  dashboard: AgentDashboardDto;
  /** null → list unavailable; the overview degrades to the dashboard stats. */
  viewings: ViewingRequestDto[] | null;
  pendingWalkthroughs: number | null;
}

// The dashboard stats are the backbone; the queues degrade to fallbacks.
async function loadOverview(): Promise<OverviewData> {
  const [dashboard, viewings, pending] = await Promise.allSettled([
    getAgentDashboard(), getMyViewingRequests(), getPendingWalkthroughs(),
  ]);
  if (dashboard.status === 'rejected') throw dashboard.reason;
  return {
    dashboard: dashboard.value,
    viewings: viewings.status === 'fulfilled' ? viewings.value : null,
    pendingWalkthroughs: pending.status === 'fulfilled' ? pending.value.length : null,
  };
}

const QUICK_ACTIONS = [
  { label: 'Review walkthroughs', to: '/agent/walkthroughs', icon: <FileIcon size={16} /> },
  { label: 'Viewing requests', to: '/agent/viewings', icon: <CalendarIcon size={16} /> },
  { label: 'My profile', to: '/agent/profile', icon: <UserIcon size={16} /> },
];

function Overview({ data }: { data: OverviewData }) {
  const session = useSession();
  const firstName = (session?.name ?? 'there').split(' ')[0];
  const { dashboard: d, viewings, pendingWalkthroughs } = data;

  const pendingViewings = viewings?.filter((v) => v.status === 'Pending') ?? null;
  const upcoming = viewings
    ?.filter((v) => v.status === 'Pending' || v.status === 'Confirmed')
    .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
    .slice(0, 5) ?? [];

  return (
    <div className="space-y-6">
      <div className="tn-rise">
        <h1 className="text-3xl font-bold tracking-tight text-ink">Welcome back, {firstName}</h1>
        <p className="mt-1 text-muted">Here's what needs your attention today.</p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          index={0}
          to="/agent/walkthroughs"
          icon={<ShieldIcon size={18} />}
          label="Walkthroughs to review"
          value={pendingWalkthroughs ?? '—'}
          sub={<span className="text-muted">Awaiting a decision</span>}
        />
        <StatCard
          index={1}
          to="/agent/viewings"
          icon={<CalendarIcon size={18} />}
          label="Viewing requests"
          value={pendingViewings ? pendingViewings.length : '—'}
          sub={<span className="text-muted">{viewings ? `${viewings.length} total` : 'Not available yet'}</span>}
        />
        <StatCard
          index={2}
          to="/agent/walkthroughs"
          icon={<FileIcon size={18} />}
          label="Walkthroughs recorded"
          value={d.totalWalkthroughs}
          sub={<span className="text-muted">{d.recentWalkthroughsCount} in the last 30 days</span>}
        />
        <StatCard
          index={3}
          to="/agent/walkthroughs"
          icon={<KeyIcon size={18} />}
          label="Properties covered"
          value={d.propertiesWithWalkthroughs}
          sub={<span className="text-muted">{d.propertiesWithoutWalkthroughs} still uncovered</span>}
        />
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
        <section className="min-w-0">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-xl font-bold text-ink">Upcoming viewings</h2>
            <Link to="/agent/viewings" className="text-sm font-semibold text-brand no-underline">View all</Link>
          </div>
          {viewings === null ? (
            <Card className="border-dashed p-10 text-center">
              <p className="text-sm text-muted">Viewing requests are unavailable right now.</p>
            </Card>
          ) : upcoming.length === 0 ? (
            <Card className="border-dashed p-10 text-center">
              <p className="font-semibold text-ink">No viewings scheduled</p>
              <p className="mt-1 text-sm text-muted">
                Tenants book viewings with you from the agents directory — keep your profile up to date.
              </p>
            </Card>
          ) : (
            <div className="space-y-3">
              {upcoming.map((v) => (
                <Card key={v.viewingRequestId} className="tn-lift flex items-center gap-4 p-4">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
                    <CalendarIcon size={18} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-ink">{formatIsoDateFull(v.scheduledAt)}</p>
                    <p className="truncate text-xs text-muted">{v.notes || `Tenant ${v.tenantId.slice(0, 8)}`}</p>
                  </div>
                  <Badge tone={v.status === 'Confirmed' ? 'green' : 'amber'}>{v.status}</Badge>
                </Card>
              ))}
            </div>
          )}
        </section>

        <aside className="min-w-0 space-y-5">
          <Card className="p-5">
            <h3 className="mb-3 font-bold text-ink">Quick actions</h3>
            <div className="grid grid-cols-1 gap-2">
              {QUICK_ACTIONS.map((a) => (
                <Link
                  key={a.to}
                  to={a.to}
                  className="flex items-center gap-2 rounded-lg border border-gray-100 px-3 py-2 text-sm font-medium text-gray-700 no-underline transition-colors hover:border-brand-50 hover:bg-brand-50/50 hover:text-brand"
                >
                  <span className="text-brand">{a.icon}</span> {a.label}
                </Link>
              ))}
            </div>
          </Card>

          <Card className="p-5">
            <h3 className="font-bold text-ink">Recent activity</h3>
            <p className="mt-2 text-sm text-muted">
              {d.recentActivity.lastWalkthroughDate
                ? `Last walkthrough on ${formatIsoDateFull(d.recentActivity.lastWalkthroughDate)} · ${d.recentActivity.totalVideoHours} hours of video recorded.`
                : 'No walkthroughs recorded yet.'}
            </p>
          </Card>

          <Card className="border-ink! bg-ink! p-5 text-white">
            <h3 className="font-bold">Get found by tenants</h3>
            <p className="mt-1 text-sm text-white/70">
              A complete directory profile brings you more viewing requests.
            </p>
            <Link to="/agent/profile" className="no-underline">
              <Button className="mt-3 rounded-xl bg-white! text-ink! hover:bg-white/90!" size="sm">
                Update profile <ChevronRightIcon size={14} />
              </Button>
            </Link>
          </Card>
        </aside>
      </div>
    </div>
  );
}

export default function AgentHomePage() {
  const state = useAsync(loadOverview, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading your workspace…" errorMessage="Failed to load your dashboard.">
      {(data) => <Overview data={data} />}
    </AsyncBoundary>
  );
}
