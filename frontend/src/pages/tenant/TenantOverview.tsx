import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { agreementsApi, bookingsApi, wishlistApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard, Row } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { BookingStatusPill } from '@/components/badges';
import { Calendar, Heart, Doc, Shield, Home } from '@/components/icons';
import { money, fmtDate } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { BookingStatus } from '@/lib/enums';

export default function TenantOverview() {
  const { user } = useAuth();
  const bookings = useQuery({ queryKey: ['my-bookings'], queryFn: bookingsApi.mine, enabled: !!user });
  const saved = useQuery({ queryKey: ['wishlist'], queryFn: wishlistApi.mine, enabled: !!user });
  const agreements = useQuery({ queryKey: ['agreements'], queryFn: agreementsApi.mine, enabled: !!user });
  const props = usePropertyLookup('all');

  const list = bookings.data ?? [];
  const upcoming = list.filter((b) => b.status === BookingStatus.Confirmed || b.status === BookingStatus.Pending);

  return (
    <div>
      <PageHeader
        title={`Welcome back, ${user?.fullName?.split(' ')[0] ?? ''}`}
        subtitle="Your trips, agreements and saved stays at a glance."
        action={
          <Link to="/search">
            <Button>
              <Home className="h-4 w-4" /> Find a stay
            </Button>
          </Link>
        }
      />

      {!user?.isVerified && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gold-600/30 bg-gold-500/10 p-4">
          <div className="flex items-center gap-3">
            <Shield className="h-5 w-5 text-gold-700" />
            <p className="text-sm font-semibold text-ink">Verify your identity to unlock bookings & agreements.</p>
          </div>
          <Link to="/verification">
            <Button size="sm" variant="gold">
              Verify now
            </Button>
          </Link>
        </div>
      )}

      <StatGrid>
        <StatCard label="Trips" value={list.length} icon={<Calendar className="h-4 w-4" />} />
        <StatCard label="Upcoming" value={upcoming.length} icon={<Calendar className="h-4 w-4" />} tone="success" />
        <StatCard label="Saved" value={saved.data?.length ?? 0} icon={<Heart className="h-4 w-4" />} tone="gold" />
        <StatCard label="Agreements" value={agreements.data?.length ?? 0} icon={<Doc className="h-4 w-4" />} tone="muted" />
      </StatGrid>

      <div className="mt-6">
        <SectionCard title="Recent trips" action={<Link to="/dashboard/trips" className="text-sm font-bold text-brand-700">View all</Link>}>
          {list.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted">No trips yet — your next stay starts with a search.</p>
          ) : (
            <div className="space-y-2.5">
              {list.slice(0, 4).map((b) => (
                <Row
                  key={b.bookingId}
                  icon={<Home className="h-5 w-5" />}
                  title={props.get(b.propertyId)?.title ?? 'Property'}
                  subtitle={`${fmtDate(b.checkInDate)} → ${fmtDate(b.checkOutDate)}`}
                  meta={
                    <div className="flex flex-col items-end gap-1">
                      <span className="font-bold">{money(b.totalAmount)}</span>
                      <BookingStatusPill status={b.status} />
                    </div>
                  }
                />
              ))}
            </div>
          )}
        </SectionCard>
      </div>
    </div>
  );
}
