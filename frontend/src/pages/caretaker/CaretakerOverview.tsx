import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { caretakersApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard, Row } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { ServiceStatusPill } from '@/components/badges';
import { Wrench, Check, Shield } from '@/components/icons';
import { fmtDate } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';

export default function CaretakerOverview() {
  const { user } = useAuth();
  const requests = useQuery({ queryKey: ['service-requests'], queryFn: caretakersApi.myServiceRequests, enabled: !!user });

  const list = requests.data ?? [];
  const open = list.filter((r) => r.status === 'Pending' || r.status === 'Accepted' || r.status === 'InProgress');
  const done = list.filter((r) => r.status === 'Completed');

  return (
    <div>
      <PageHeader title="Caretaker dashboard" subtitle="Stay on top of the homes and jobs in your care." />

      {!user?.isVerified && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gold-600/30 bg-gold-500/10 p-4">
          <div className="flex items-center gap-3">
            <Shield className="h-5 w-5 text-gold-700" />
            <p className="text-sm font-semibold text-ink">Verify your identity to take on service requests.</p>
          </div>
          <Link to="/verification">
            <Button size="sm" variant="gold">
              Verify now
            </Button>
          </Link>
        </div>
      )}

      <StatGrid>
        <StatCard label="Open jobs" value={open.length} icon={<Wrench className="h-4 w-4" />} tone="gold" />
        <StatCard label="Completed" value={done.length} icon={<Check className="h-4 w-4" />} tone="success" />
        <StatCard label="Total" value={list.length} icon={<Wrench className="h-4 w-4" />} tone="muted" />
        <StatCard label="This month" value={done.length} icon={<Check className="h-4 w-4" />} />
      </StatGrid>

      <div className="mt-6">
        <SectionCard title="Recent requests" action={<Link to="/caretaker/requests" className="text-sm font-bold text-brand-700">View all</Link>}>
          {list.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted">No service requests yet.</p>
          ) : (
            <div className="space-y-2.5">
              {list.slice(0, 5).map((r) => (
                <Row
                  key={r.serviceRequestId}
                  icon={<Wrench className="h-5 w-5" />}
                  title={r.serviceType}
                  subtitle={`${r.description} · ${fmtDate(r.createdAt)}`}
                  meta={<ServiceStatusPill status={r.status} />}
                />
              ))}
            </div>
          )}
        </SectionCard>
      </div>
    </div>
  );
}
