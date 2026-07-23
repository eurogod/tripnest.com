import { Link } from 'react-router-dom';
import { getAdminStats, type AdminStatsDto } from '../../api/roleDashboards';
import { getSupportTickets, getAuditLogs, type AuditLogDto } from '../../api/adminWorkspace';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import StatCard from '../../components/workspace/StatCard';
import Card from '../../components/ui/Card';
import { formatCurrency } from '../../lib/format';
import { formatIsoDateFull } from '../../api/backend';
import { useSession } from '../../store/authStore';
import {
  UsersIcon, KeyIcon, CalendarIcon, ShieldIcon, MessageIcon, FileIcon, ClockIcon, CardIcon,
} from '../../components/tenant/icons';

interface OverviewData {
  stats: AdminStatsDto;
  /** null → count unavailable; the card shows a dash. */
  openTickets: number | null;
  auditLogs: AuditLogDto[] | null;
}

// Platform stats are the backbone; tickets and the audit trail degrade.
async function loadOverview(): Promise<OverviewData> {
  const [stats, tickets, logs] = await Promise.allSettled([
    getAdminStats(), getSupportTickets(), getAuditLogs(8),
  ]);
  if (stats.status === 'rejected') throw stats.reason;
  return {
    stats: stats.value,
    openTickets: tickets.status === 'fulfilled' ? tickets.value.length : null,
    auditLogs: logs.status === 'fulfilled' ? logs.value : null,
  };
}

const QUICK_ACTIONS = [
  { label: 'Disputes', to: '/admin/disputes', icon: <ShieldIcon size={16} /> },
  { label: 'Support tickets', to: '/admin/tickets', icon: <MessageIcon size={16} /> },
  { label: 'Walkthrough review', to: '/admin/walkthroughs', icon: <FileIcon size={16} /> },
  { label: 'Audit logs', to: '/admin/audit', icon: <ClockIcon size={16} /> },
];

function Overview({ data }: { data: OverviewData }) {
  const session = useSession();
  const firstName = (session?.name ?? 'there').split(' ')[0];
  const { stats: s, openTickets, auditLogs } = data;

  return (
    <div className="space-y-6">
      <div className="tn-rise">
        <h1 className="text-3xl font-bold tracking-tight text-ink">Welcome back, {firstName}</h1>
        <p className="mt-1 text-muted">Here's how the platform is doing.</p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          index={0}
          to="/admin/disputes"
          icon={<ShieldIcon size={18} />}
          label="Open disputes"
          value={s.openDisputes}
          sub={<span className="text-muted">{formatCurrency(s.totalEscrowHeld)} held in escrow</span>}
        />
        <StatCard
          index={1}
          to="/admin/tickets"
          icon={<MessageIcon size={18} />}
          label="Support tickets"
          value={openTickets ?? '—'}
          sub={<span className="text-muted">Escalated by the assistant</span>}
        />
        <StatCard
          index={2}
          to="/admin/walkthroughs"
          icon={<FileIcon size={18} />}
          label="Walkthroughs to review"
          value={s.pendingWalkthroughs}
          sub={<span className="text-muted">{s.pendingVerifications} identity checks pending</span>}
        />
        <StatCard
          index={3}
          to="/admin/audit"
          icon={<UsersIcon size={18} />}
          label="Total users"
          value={s.totalUsers}
          sub={<span className="text-muted">{s.totalTenants} tenants · {s.totalLandlords} landlords</span>}
        />
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          index={4}
          to="/admin/walkthroughs"
          icon={<KeyIcon size={18} />}
          label="Active properties"
          value={`${s.activeProperties} / ${s.totalProperties}`}
          sub={<span className="text-muted">Live on the marketplace</span>}
        />
        <StatCard
          index={5}
          to="/admin/audit"
          icon={<CalendarIcon size={18} />}
          label="Bookings"
          value={s.totalBookings}
          sub={<span className="text-muted">{s.confirmedBookings} confirmed · {s.cancelledBookings} cancelled</span>}
        />
        <StatCard
          index={6}
          to="/admin/disputes"
          icon={<CardIcon size={18} />}
          label="Escrow held"
          value={formatCurrency(s.totalEscrowHeld)}
          sub={<span className="text-muted">{formatCurrency(s.totalEscrowReleased)} released</span>}
        />
        <StatCard
          index={7}
          to="/admin/audit"
          icon={<ClockIcon size={18} />}
          label="Open maintenance"
          value={s.openMaintenanceRequests}
          sub={<span className="text-muted">{s.activeServiceRequests} active service requests</span>}
        />
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
        <section className="min-w-0">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-xl font-bold text-ink">Latest activity</h2>
            <Link to="/admin/audit" className="text-sm font-semibold text-brand no-underline">View all</Link>
          </div>
          {auditLogs === null ? (
            <Card className="border-dashed p-10 text-center">
              <p className="text-sm text-muted">The audit trail is unavailable right now.</p>
            </Card>
          ) : auditLogs.length === 0 ? (
            <Card className="border-dashed p-10 text-center">
              <p className="text-sm text-muted">No platform activity recorded yet.</p>
            </Card>
          ) : (
            <Card className="divide-y divide-gray-50 p-0">
              {auditLogs.map((log) => (
                <div key={log.id} className="flex items-center gap-4 px-5 py-3">
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
                    <ClockIcon size={16} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-ink">{log.action}</p>
                    <p className="truncate text-xs text-muted">{log.entityType} {log.entityId.slice(0, 8)} · user {log.userId.slice(0, 8)}</p>
                  </div>
                  <span className="shrink-0 text-xs text-muted">{formatIsoDateFull(log.createdAt)}</span>
                </div>
              ))}
            </Card>
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
            <h3 className="font-bold text-ink">Verification queue</h3>
            <p className="mt-2 text-sm text-muted">
              {s.pendingVerifications === 0
                ? 'No identity checks waiting — the registry worker is keeping up.'
                : `${s.pendingVerifications} identity check${s.pendingVerifications === 1 ? '' : 's'} in the registry queue.`}
            </p>
          </Card>

          <Card className="border-ink! bg-ink! p-5 text-white">
            <h3 className="font-bold">Escrow at a glance</h3>
            <p className="mt-1 text-sm text-white/70">
              {formatCurrency(s.totalEscrowHeld)} protected for guests right now · {formatCurrency(s.totalEscrowReleased)} paid out to hosts all-time.
            </p>
          </Card>
        </aside>
      </div>
    </div>
  );
}

export default function AdminHomePage() {
  const state = useAsync(loadOverview, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading platform overview…" errorMessage="Failed to load platform stats.">
      {(data) => <Overview data={data} />}
    </AsyncBoundary>
  );
}
