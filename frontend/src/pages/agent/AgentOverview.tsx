import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Calendar, Check, Chat, Shield } from '@/components/icons';
import { useAuth } from '@/auth/AuthContext';

type Raw = Record<string, unknown>;
const num = (o: Raw | undefined, ...keys: string[]): number => {
  for (const k of keys) if (typeof o?.[k] === 'number') return o[k] as number;
  return 0;
};

export default function AgentOverview() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['agent-dashboard'], queryFn: dashboardApi.agent, enabled: !!user });
  const d = query.data as Raw | undefined;

  return (
    <div>
      <PageHeader title="Agent dashboard" subtitle="Manage viewing requests and help tenants see homes in person." />

      {!user?.isVerified && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gold-600/30 bg-gold-500/10 p-4">
          <div className="flex items-center gap-3">
            <Shield className="h-5 w-5 text-gold-700" />
            <p className="text-sm font-semibold text-ink">Verify your identity to accept viewing requests.</p>
          </div>
          <Link to="/verification">
            <Button size="sm" variant="gold">
              Verify now
            </Button>
          </Link>
        </div>
      )}

      <StatGrid>
        <StatCard label="Pending viewings" value={num(d, 'pendingViewings', 'pending')} icon={<Calendar className="h-4 w-4" />} tone="gold" />
        <StatCard label="Confirmed" value={num(d, 'confirmedViewings', 'confirmed')} icon={<Check className="h-4 w-4" />} tone="success" />
        <StatCard label="Completed" value={num(d, 'completedViewings', 'completed')} icon={<Check className="h-4 w-4" />} />
        <StatCard label="Properties" value={num(d, 'properties', 'totalProperties')} icon={<Calendar className="h-4 w-4" />} tone="muted" />
      </StatGrid>

      <div className="mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2">
        <SectionCard title="Viewing requests">
          <p className="text-sm text-muted">Review and confirm the times tenants want to visit.</p>
          <Link to="/agent/viewings" className="mt-4 inline-block">
            <Button>
              <Calendar className="h-4 w-4" /> Manage viewings
            </Button>
          </Link>
        </SectionCard>
        <SectionCard title="Messages">
          <p className="text-sm text-muted">Coordinate directly with tenants — safely on TripNest.</p>
          <Link to="/messages" className="mt-4 inline-block">
            <Button variant="outline">
              <Chat className="h-4 w-4" /> Open messages
            </Button>
          </Link>
        </SectionCard>
      </div>
    </div>
  );
}
