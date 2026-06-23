import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { Property } from '@/types/api';
import { money, priceLabel, propertyPhoto } from '@/lib/format';
import { StayTypeLabel } from '@/lib/enums';
import { Heart, MapPin, Star } from './icons';
import { VerifiedBadge } from './badges';
import { wishlistApi } from '@/lib/services';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from './Toast';

export function PropertyCard({
  p,
  saved,
  onHover,
  active,
}: {
  p: Property;
  saved?: boolean;
  onHover?: (id: string | null) => void;
  active?: boolean;
}) {
  const { user } = useAuth();
  const toast = useToast();
  const qc = useQueryClient();
  const price = priceLabel(p);

  const toggle = useMutation({
    mutationFn: () => (saved ? wishlistApi.remove(p.propertyId) : wishlistApi.add(p.propertyId)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['wishlist'] });
      toast.success(saved ? 'Removed from saved' : 'Saved to wishlist');
    },
    onError: () => toast.error('Could not update wishlist'),
  });

  return (
    <Link
      to={`/property/${p.propertyId}`}
      className={`group block overflow-hidden rounded-xl border bg-white transition hover:shadow-soft ${
        active ? 'border-brand-600 ring-2 ring-brand-600/20' : 'border-line'
      }`}
      onMouseEnter={() => onHover?.(p.propertyId)}
      onMouseLeave={() => onHover?.(null)}
    >
      <div className="relative aspect-[4/3] overflow-hidden bg-line">
        <img
          src={propertyPhoto(p)}
          alt={p.title}
          loading="lazy"
          className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
        />
        <div className="absolute left-3 top-3">
          <VerifiedBadge size="sm" />
        </div>
        {user && (
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault();
              toggle.mutate();
            }}
            className="absolute right-3 top-3 grid h-9 w-9 place-items-center rounded-full bg-white/90 text-ink shadow-card backdrop-blur transition hover:scale-105"
            aria-label={saved ? 'Remove from saved' : 'Save'}
          >
            <Heart className={`h-5 w-5 ${saved ? 'fill-danger text-danger' : ''}`} />
          </button>
        )}
        <span className="pill absolute bottom-3 left-3 bg-ink/75 text-white">{StayTypeLabel[p.stayType]}</span>
      </div>
      <div className="p-3.5">
        <div className="flex items-start justify-between gap-2">
          <h3 className="line-clamp-1 font-bold text-ink">{p.title}</h3>
          <span className="inline-flex shrink-0 items-center gap-1 text-sm font-semibold">
            <Star className="h-4 w-4 text-gold-500" />
            New
          </span>
        </div>
        <p className="mt-0.5 flex items-center gap-1 text-sm text-muted">
          <MapPin className="h-3.5 w-3.5" />
          <span className="line-clamp-1">{p.location}</span>
        </p>
        <p className="mt-1 text-xs text-muted">
          {p.bedrooms} bed · {p.bathrooms} bath · {p.propertyType}
        </p>
        <p className="mt-2 font-bold text-ink">
          {money(price.amount)} <span className="text-sm font-medium text-muted">{price.unit}</span>
        </p>
      </div>
    </Link>
  );
}

export function PropertyCardSkeleton() {
  return (
    <div className="overflow-hidden rounded-xl border border-line bg-white">
      <div className="aspect-[4/3] animate-pulse bg-line" />
      <div className="space-y-2 p-3.5">
        <div className="h-4 w-2/3 animate-pulse rounded bg-line" />
        <div className="h-3 w-1/2 animate-pulse rounded bg-line" />
        <div className="h-4 w-1/3 animate-pulse rounded bg-line" />
      </div>
    </div>
  );
}
