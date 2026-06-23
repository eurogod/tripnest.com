import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard, Async } from '@/components/dashboard';
import { Users, Shield } from '@/components/icons';
import { useAuth } from '@/auth/AuthContext';
import type { AdminStats } from '@/types/api';

function Bar({ label, value, total, tone }: { label: string; value: number; total: number; tone: string }) {
  const pct = total > 0 ? Math.round((value / total) * 100) : 0;
  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-sm">
        <span className="font-semibold text-ink">{label}</span>
        <span className="text-muted">
          {value} · {pct}%
        </span>
      </div>
      <div className="h-2.5 w-full overflow-hidden rounded-full bg-line">
        <div className={`h-full rounded-full ${tone}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export default function AdminUsers() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['admin-stats'], queryFn: dashboardApi.adminStats, enabled: !!user });

  return (
    <div>
      <PageHeader title="Users" subtitle="Who’s on TripNest and how verified the community is." />
      <Async query={query} isEmpty={() => false}>
        {(s: AdminStats) => {
          const verifiedPct = s.totalUsers > 0 ? Math.round((s.verifiedUsers / s.totalUsers) * 100) : 0;
          return (
            <div className="space-y-6">
              <StatGrid>
                <StatCard label="Total users" value={s.totalUsers} icon={<Users className="h-4 w-4" />} />
                <StatCard label="Verified" value={s.verifiedUsers} hint={`${verifiedPct}% of users`} icon={<Shield className="h-4 w-4" />} tone="success" />
                <StatCard label="Pending" value={s.pendingVerifications} icon={<Shield className="h-4 w-4" />} tone="gold" />
                <StatCard label="Avg trust" value={Math.round(s.averageTrustScore)} icon={<Shield className="h-4 w-4" />} tone="muted" />
              </StatGrid>

              <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
                <SectionCard title="By role">
                  <div className="space-y-4">
                    <Bar label="Tenants" value={s.totalTenants} total={s.totalUsers} tone="bg-brand-600" />
                    <Bar label="Landlords" value={s.totalLandlords} total={s.totalUsers} tone="bg-gold-600" />
                    <Bar label="Agents" value={s.totalAgents} total={s.totalUsers} tone="bg-brand-400" />
                    <Bar label="Caretakers" value={s.totalCaretakers} total={s.totalUsers} tone="bg-success" />
                  </div>
                </SectionCard>

                <SectionCard title="Verification">
                  <div className="space-y-4">
                    <Bar label="Verified" value={s.verifiedUsers} total={s.totalUsers} tone="bg-success" />
                    <Bar label="Pending review" value={s.pendingVerifications} total={s.totalUsers} tone="bg-gold-500" />
                  </div>
                  <p className="mt-5 rounded-lg bg-surface p-3 text-sm text-muted">
                    Identity verification is the backbone of trust on TripNest — {verifiedPct}% of users have a confirmed
                    Ghana Card.
                  </p>
                </SectionCard>
              </div>
            </div>
          );
        }}
      </Async>
    </div>
  );
}
