import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard, Row } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { PropertyStatusPill } from '@/components/badges';
import { MapPin, Calendar, Cash, Plus, Shield, Home } from '@/components/icons';
import { money } from '@/lib/format';
import { useMyProperties } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { PropertyStatus } from '@/lib/enums';

const num = (o: Record<string, unknown> | undefined, ...keys: string[]): number => {
  for (const k of keys) {
    const v = o?.[k];
    if (typeof v === 'number') return v;
  }
  return 0;
};

export default function HostOverview() {
  const { user } = useAuth();
  const listings = useMyProperties();
  const stats = useQuery({ queryKey: ['landlord-stats'], queryFn: dashboardApi.landlordStats, enabled: !!user });

  const props = listings.data ?? [];
  const active = props.filter((p) => p.status === PropertyStatus.Active).length;
  const s = stats.data;

  return (
    <div>
      <PageHeader
        title="Host dashboard"
        subtitle="Your listings, reservations and escrow earnings."
        action={
          <Link to="/host/listings/new">
            <Button>
              <Plus className="h-4 w-4" /> Add property
            </Button>
          </Link>
        }
      />

      {!user?.isVerified && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gold-600/30 bg-gold-500/10 p-4">
          <div className="flex items-center gap-3">
            <Shield className="h-5 w-5 text-gold-700" />
            <p className="text-sm font-semibold text-ink">Verify your Ghana Card to publish listings and accept bookings.</p>
          </div>
          <Link to="/verification">
            <Button size="sm" variant="gold">
              Verify now
            </Button>
          </Link>
        </div>
      )}

      <StatGrid>
        <StatCard label="Listings" value={props.length} icon={<MapPin className="h-4 w-4" />} />
        <StatCard label="Active" value={active} icon={<Home className="h-4 w-4" />} tone="success" />
        <StatCard label="Reservations" value={num(s, 'totalBookings', 'reservations', 'bookings')} icon={<Calendar className="h-4 w-4" />} tone="gold" />
        <StatCard label="In escrow" value={money(num(s, 'totalEscrowHeld', 'escrowHeld', 'inEscrow'))} icon={<Cash className="h-4 w-4" />} tone="muted" />
      </StatGrid>

      <div className="mt-6 grid grid-cols-1 gap-5 lg:grid-cols-2">
        <SectionCard title="Your listings" action={<Link to="/host/listings" className="text-sm font-bold text-brand-700">Manage</Link>}>
          {props.length === 0 ? (
            <div className="py-6 text-center">
              <p className="text-sm text-muted">No listings yet.</p>
              <Link to="/host/listings/new" className="mt-3 inline-block">
                <Button size="sm">Create your first listing</Button>
              </Link>
            </div>
          ) : (
            <div className="space-y-2.5">
              {props.slice(0, 5).map((p) => (
                <Row
                  key={p.propertyId}
                  icon={<Home className="h-5 w-5" />}
                  title={p.title}
                  subtitle={p.location}
                  meta={<PropertyStatusPill status={p.status} />}
                />
              ))}
            </div>
          )}
        </SectionCard>

        <SectionCard title="Quick actions">
          <div className="grid grid-cols-1 gap-3">
            <Link to="/host/reservations">
              <Button variant="outline" block>
                <Calendar className="h-4 w-4" /> View reservations
              </Button>
            </Link>
            <Link to="/host/earnings">
              <Button variant="outline" block>
                <Cash className="h-4 w-4" /> Earnings & escrow
              </Button>
            </Link>
            <Link to="/messages">
              <Button variant="outline" block>
                Messages
              </Button>
            </Link>
          </div>
        </SectionCard>
      </div>
    </div>
  );
}
