import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Users, Shield, Home, Calendar, Cash, Camera, Wrench } from '@/components/icons';
import { money } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';

export default function AdminOverview() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['admin-stats'], queryFn: dashboardApi.adminStats, enabled: !!user });

  return (
    <div>
      <PageHeader title="Admin control center" subtitle="Platform health, trust and the moderation queues that keep TripNest safe." />
      <Async query={query} isEmpty={() => false}>
        {(s) => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Users" value={s.totalUsers} icon={<Users className="h-4 w-4" />} hint={`${s.verifiedUsers} verified`} />
              <StatCard label="Pending verifications" value={s.pendingVerifications} icon={<Shield className="h-4 w-4" />} tone="gold" />
              <StatCard label="Properties" value={s.totalProperties} icon={<Home className="h-4 w-4" />} hint={`${s.activeProperties} active`} tone="success" />
              <StatCard label="Avg trust score" value={Math.round(s.averageTrustScore)} icon={<Shield className="h-4 w-4" />} tone="muted" />
            </StatGrid>

            <StatGrid>
              <StatCard label="Bookings" value={s.totalBookings} icon={<Calendar className="h-4 w-4" />} hint={`${s.completedBookings} completed`} />
              <StatCard label="Escrow held" value={money(s.totalEscrowHeld)} icon={<Cash className="h-4 w-4" />} tone="gold" />
              <StatCard label="Escrow released" value={money(s.totalEscrowReleased)} icon={<Cash className="h-4 w-4" />} tone="success" />
              <StatCard label="Open disputes" value={s.openDisputes} icon={<Cash className="h-4 w-4" />} tone={s.openDisputes > 0 ? 'danger' : 'muted'} />
            </StatGrid>

            <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">
              <SectionCard title="Walkthrough reviews">
                <p className="text-3xl font-extrabold text-brand-600">{s.pendingWalkthroughs}</p>
                <p className="mt-1 text-sm text-muted">videos awaiting approval before listings go live.</p>
                <Link to="/admin/walkthroughs" className="mt-4 inline-block">
                  <Button size="sm">
                    <Camera className="h-4 w-4" /> Review queue
                  </Button>
                </Link>
              </SectionCard>
              <SectionCard title="Disputes">
                <p className="text-3xl font-extrabold text-danger">{s.openDisputes}</p>
                <p className="mt-1 text-sm text-muted">escrow disputes need a decision.</p>
                <Link to="/admin/disputes" className="mt-4 inline-block">
                  <Button size="sm" variant="outline">
                    <Cash className="h-4 w-4" /> Resolve
                  </Button>
                </Link>
              </SectionCard>
              <SectionCard title="Operations">
                <ul className="space-y-2 text-sm text-muted">
                  <li className="flex items-center gap-2">
                    <Wrench className="h-4 w-4 text-brand-600" /> {s.openMaintenanceRequests} open maintenance
                  </li>
                  <li className="flex items-center gap-2">
                    <Wrench className="h-4 w-4 text-brand-600" /> {s.activeServiceRequests} active service jobs
                  </li>
                  <li className="flex items-center gap-2">
                    <Users className="h-4 w-4 text-brand-600" /> {s.totalAgents} agents · {s.totalCaretakers} caretakers
                  </li>
                </ul>
                <Link to="/admin/users" className="mt-4 inline-block">
                  <Button size="sm" variant="outline">
                    <Users className="h-4 w-4" /> User breakdown
                  </Button>
                </Link>
              </SectionCard>
            </div>
          </div>
        )}
      </Async>
    </div>
  );
}
