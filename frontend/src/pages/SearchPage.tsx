import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { propertiesApi, wishlistApi } from '@/lib/services';
import { PropertyCard, PropertyCardSkeleton } from '@/components/PropertyCard';
import { ResultsMap, type MapPoint } from '@/components/PropertyMap';
import { Button, EmptyState } from '@/components/ui';
import { MapPin, Sliders, Search as SearchIcon } from '@/components/icons';
import { priceLabel } from '@/lib/format';
import { StayType } from '@/lib/enums';
import { useAuth } from '@/auth/AuthContext';
import type { Property } from '@/types/api';

export default function SearchPage() {
  const [params, setParams] = useSearchParams();
  const { user } = useAuth();
  const location = params.get('location') ?? '';
  const [showMapMobile, setShowMapMobile] = useState(false);
  const [activeId, setActiveId] = useState<string | null>(null);

  // Filters
  const [minBeds, setMinBeds] = useState<number | ''>('');
  const [maxPrice, setMaxPrice] = useState<number | ''>('');
  const [stayType, setStayType] = useState<number | ''>('');
  const [verifiedOnly, setVerifiedOnly] = useState(false);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['search', location, minBeds],
    queryFn: () =>
      location || minBeds !== ''
        ? propertiesApi.search({ location: location || undefined, minBedrooms: minBeds === '' ? undefined : minBeds })
        : propertiesApi.list(),
  });

  const { data: saved } = useQuery({
    queryKey: ['wishlist'],
    queryFn: wishlistApi.mine,
    enabled: !!user,
  });
  const savedIds = useMemo(() => new Set((saved ?? []).map((p) => p.propertyId)), [saved]);

  const results = useMemo(() => {
    let list: Property[] = data ?? [];
    if (maxPrice !== '') list = list.filter((p) => priceLabel(p).amount <= maxPrice);
    if (stayType !== '') list = list.filter((p) => p.stayType === stayType);
    // "Verified only" — all hosts are verified to publish, so this is a no-op filter placeholder kept for UX parity.
    return list;
  }, [data, maxPrice, stayType]);

  const points: MapPoint[] = results.map((p) => ({
    id: p.propertyId,
    lat: p.latitude,
    lng: p.longitude,
    price: priceLabel(p).amount,
    title: p.title,
  }));

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col">
      {/* Filters bar */}
      <div className="border-b border-line bg-white">
        <div className="container-tn flex items-center gap-3 overflow-x-auto py-3">
          <div className="flex items-center gap-1.5 rounded-full border border-line px-3 py-1.5 text-sm font-semibold">
            <MapPin className="h-4 w-4 text-brand-600" />
            <input
              value={location}
              onChange={(e) => setParams((p) => { p.set('location', e.target.value); return p; }, { replace: true })}
              placeholder="Anywhere in Ghana"
              className="w-36 bg-transparent outline-none"
            />
          </div>
          <Select value={minBeds} onChange={setMinBeds} label="Beds" options={[1, 2, 3, 4]} suffix="+ beds" />
          <Select value={stayType} onChange={(v) => setStayType(v)} label="Stay" custom={[
            { v: StayType.ShortTerm, l: 'Short stay' },
            { v: StayType.LongTerm, l: 'Long-term' },
            { v: StayType.Student, l: 'Student' },
          ]} />
          <Select value={maxPrice} onChange={setMaxPrice} label="Max price" options={[500, 1000, 2000, 5000]} prefix="≤ GHS " />
          <button
            onClick={() => setVerifiedOnly((v) => !v)}
            className={`flex items-center gap-1.5 whitespace-nowrap rounded-full border px-3 py-1.5 text-sm font-semibold ${verifiedOnly ? 'border-brand-600 bg-brand-50 text-brand-700' : 'border-line'}`}
          >
            ✓ Verified hosts
          </button>
          <span className="ml-auto hidden text-sm text-muted sm:block">{results.length} homes</span>
        </div>
      </div>

      {/* Split view */}
      <div className="flex min-h-0 flex-1">
        <div className={`min-h-0 flex-1 overflow-y-auto ${showMapMobile ? 'hidden' : 'block'} lg:block`}>
          <div className="container-tn py-5">
            <h1 className="mb-4 text-lg font-bold">
              {location ? `Homes in ${location}` : 'Explore homes across Ghana'}
            </h1>
            {isError ? (
              <EmptyState title="Couldn't load homes" subtitle="Check your connection and try again." action={<Button onClick={() => refetch()}>Retry</Button>} />
            ) : isLoading ? (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                {Array.from({ length: 6 }).map((_, i) => <PropertyCardSkeleton key={i} />)}
              </div>
            ) : results.length ? (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                {results.map((p) => (
                  <PropertyCard key={p.propertyId} p={p} saved={savedIds.has(p.propertyId)} onHover={setActiveId} active={activeId === p.propertyId} />
                ))}
              </div>
            ) : (
              <EmptyState
                icon={<SearchIcon />}
                title="No homes match your search"
                subtitle="Try a different location or relax your filters."
                action={<Button variant="outline" onClick={() => { setMinBeds(''); setMaxPrice(''); setStayType(''); }}>Clear filters</Button>}
              />
            )}
          </div>
        </div>

        {/* Map */}
        <div className={`${showMapMobile ? 'block' : 'hidden'} w-full lg:block lg:w-[44%] lg:max-w-[640px]`}>
          <div className="h-full">
            <ResultsMap points={points} activeId={activeId} onSelect={setActiveId} />
          </div>
        </div>
      </div>

      {/* Mobile map toggle */}
      <button
        onClick={() => setShowMapMobile((s) => !s)}
        className="btn-primary fixed bottom-5 left-1/2 z-40 -translate-x-1/2 lg:hidden"
      >
        <Sliders className="h-4 w-4" />
        {showMapMobile ? 'Show list' : 'Show map'}
      </button>
    </div>
  );
}

function Select<T extends number | ''>({
  value,
  onChange,
  label,
  options,
  custom,
  prefix = '',
  suffix = '',
}: {
  value: T;
  onChange: (v: T) => void;
  label: string;
  options?: number[];
  custom?: { v: number; l: string }[];
  prefix?: string;
  suffix?: string;
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange((e.target.value === '' ? '' : Number(e.target.value)) as T)}
      className="whitespace-nowrap rounded-full border border-line bg-white px-3 py-1.5 text-sm font-semibold outline-none focus:border-brand-600"
    >
      <option value="">{label}</option>
      {custom
        ? custom.map((o) => <option key={o.v} value={o.v}>{o.l}</option>)
        : options?.map((o) => <option key={o} value={o}>{prefix}{o}{suffix}</option>)}
    </select>
  );
}
