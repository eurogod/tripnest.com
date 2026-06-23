import { useQuery } from '@tanstack/react-query';
import { wishlistApi } from '@/lib/services';
import { PropertyCard } from '@/components/PropertyCard';
import { PageHeader, Async } from '@/components/dashboard';
import { Heart } from '@/components/icons';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';
import { useAuth } from '@/auth/AuthContext';

export default function WishlistPage() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['wishlist'], queryFn: wishlistApi.mine, enabled: !!user });

  return (
    <div className="container-tn max-w-6xl py-8">
      <PageHeader title="Saved stays" subtitle="Homes you’ve bookmarked. Verified and ready when you are." />
      <Async
        query={query}
        emptyIcon={<Heart className="h-6 w-6" />}
        emptyTitle="No saved stays yet"
        emptySubtitle="Tap the heart on any listing to keep it here for later."
        emptyAction={
          <Link to="/search">
            <Button>Browse stays</Button>
          </Link>
        }
      >
        {(items) => (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((p) => (
              <PropertyCard key={p.propertyId} p={p} saved />
            ))}
          </div>
        )}
      </Async>
    </div>
  );
}
