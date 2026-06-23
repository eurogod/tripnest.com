import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  bookingsApi,
  escrowApi,
  propertiesApi,
  reviewsApi,
  trustApi,
} from '@/lib/services';
import { Button, ErrorState, Skeleton } from '@/components/ui';
import { Avatar, StarRating, TrustChip, VerifiedBadge, Pill } from '@/components/badges';
import { Heart, MapPin, Shield, Wifi, Chat as ChatIcon, Star, Camera } from '@/components/icons';
import { SingleMap } from '@/components/PropertyMap';
import { money, parseAmenities, parsePhotos, fallbackPhoto, fmtDate, nights, priceLabel } from '@/lib/format';
import { CancellationPolicyLabel, StayType, StayTypeLabel } from '@/lib/enums';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';

export default function PropertyDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const toast = useToast();

  const { data: property, isLoading, isError, refetch } = useQuery({
    queryKey: ['property', id],
    queryFn: () => propertiesApi.get(id),
  });
  const { data: trust } = useQuery({ queryKey: ['trust', 'property', id], queryFn: () => trustApi.forProperty(id) });
  const { data: reviews } = useQuery({ queryKey: ['reviews', id], queryFn: () => reviewsApi.forProperty(id) });

  const photos = useMemo(() => {
    const real = parsePhotos(property?.photoPaths);
    return real.length ? real : [fallbackPhoto(id), fallbackPhoto(id + 'a'), fallbackPhoto(id + 'b'), fallbackPhoto(id + 'c'), fallbackPhoto(id + 'd')];
  }, [property?.photoPaths, id]);

  const avgRating = reviews?.items.length
    ? reviews.items.reduce((s, r) => s + r.rating, 0) / reviews.items.length
    : null;

  if (isLoading) return <DetailSkeleton />;
  if (isError || !property) return <div className="container-tn py-16"><ErrorState message="This property could not be loaded." onRetry={refetch} /></div>;

  const amenities = parseAmenities(property.amenities);

  return (
    <div className="container-tn py-6">
      {/* Title row */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-extrabold">{property.title}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-3 text-sm text-muted">
            {avgRating != null && <StarRating value={avgRating} count={reviews?.totalCount} />}
            <span className="flex items-center gap-1"><MapPin className="h-4 w-4" /> {property.location}</span>
            <VerifiedBadge size="sm" />
          </div>
        </div>
        <button className="btn-outline btn-sm"><Heart className="h-4 w-4" /> Save</button>
      </div>

      {/* Gallery */}
      <div className="mt-4 grid grid-cols-1 gap-2 overflow-hidden rounded-2xl sm:grid-cols-4 sm:grid-rows-2" style={{ height: 420 }}>
        <img src={photos[0]} alt={property.title} className="h-full w-full object-cover sm:col-span-2 sm:row-span-2" />
        {photos.slice(1, 5).map((src, i) => (
          <img key={i} src={src} alt="" className="hidden h-full w-full object-cover sm:block" />
        ))}
      </div>

      <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_380px]">
        {/* Left column */}
        <div>
          {/* Quick facts */}
          <div className="flex flex-wrap items-center gap-2 border-b border-line pb-6">
            <Pill tone="info">{StayTypeLabel[property.stayType]}</Pill>
            <Pill tone="mut">{property.bedrooms} bedrooms</Pill>
            <Pill tone="mut">{property.bathrooms} bathrooms</Pill>
            <Pill tone="mut">{property.propertyType}</Pill>
            <Pill tone="warn">{CancellationPolicyLabel[property.cancellationPolicy]} cancellation</Pill>
          </div>

          {/* Host + reality score */}
          <section className="border-b border-line py-6">
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-3">
                <Avatar name="Verified Host" size={52} />
                <div>
                  <p className="flex items-center gap-1.5 font-bold">Verified host <VerifiedBadge size="sm" label="" /></p>
                  <p className="text-sm text-muted">Ghana Card verified · Member on TripNest</p>
                </div>
              </div>
              {trust && <TrustChip score={trust.finalScore} label={trust.label} />}
            </div>

            {trust && (
              <div className="mt-5 rounded-xl border border-line bg-surface p-4">
                <p className="flex items-center gap-2 text-sm font-bold">
                  <Shield className="h-4 w-4 text-brand-600" /> Reality score · {Math.round(trust.finalScore)}/100
                  <span className="font-medium text-muted">({trust.trend})</span>
                </p>
                <p className="mt-1 text-xs text-muted">
                  TripNest's advanced trust signal — blends identity verification, booking history and guest feedback.
                </p>
                <div className="mt-3 space-y-2">
                  <ScoreBar label="Identity & verification" value={Number(trust.verificationComponent)} />
                  <ScoreBar label="Booking history" value={Number(trust.historyComponent)} />
                  <ScoreBar label="Guest feedback" value={Number(trust.feedbackComponent)} />
                </div>
              </div>
            )}
          </section>

          {/* Description */}
          <section className="border-b border-line py-6">
            <h2 className="mb-2 text-lg font-bold">About this home</h2>
            <p className="whitespace-pre-line text-sm leading-relaxed text-ink/90">{property.description}</p>
          </section>

          {/* Amenities */}
          {amenities.length > 0 && (
            <section className="border-b border-line py-6">
              <h2 className="mb-3 text-lg font-bold">What this place offers</h2>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                {amenities.map((a) => (
                  <span key={a} className="flex items-center gap-2 text-sm">
                    <Wifi className="h-4 w-4 text-brand-600" /> {a}
                  </span>
                ))}
              </div>
            </section>
          )}

          {/* Map */}
          <section className="border-b border-line py-6">
            <h2 className="mb-3 text-lg font-bold">Where you'll be</h2>
            <p className="mb-3 text-sm text-muted">{property.location}</p>
            <div className="h-72 overflow-hidden rounded-xl border border-line">
              <SingleMap lat={property.latitude} lng={property.longitude} title={property.title} />
            </div>
          </section>

          {/* Reviews & comments */}
          <section className="py-6">
            <h2 className="mb-4 flex items-center gap-2 text-lg font-bold">
              <Star className="h-5 w-5 text-gold-500" />
              {avgRating != null ? `${avgRating.toFixed(1)} · ${reviews?.totalCount} reviews` : 'Reviews'}
            </h2>
            {reviews?.items.length ? (
              <div className="grid gap-4 sm:grid-cols-2">
                {reviews.items.map((r) => (
                  <div key={r.reviewId} className="card p-4">
                    <div className="flex items-center gap-3">
                      <Avatar name={r.reviewerId} size={36} />
                      <div>
                        <p className="text-sm font-bold">Guest</p>
                        <p className="text-xs text-muted">{fmtDate(r.createdAt)}</p>
                      </div>
                      <StarRating value={r.rating} className="ml-auto" />
                    </div>
                    <p className="mt-3 text-sm text-ink/90">{r.comment}</p>
                  </div>
                ))}
              </div>
            ) : (
              <p className="rounded-xl border border-dashed border-line py-8 text-center text-sm text-muted">
                No reviews yet — be the first after your stay.
              </p>
            )}
          </section>
        </div>

        {/* Booking widget */}
        <aside className="lg:sticky lg:top-20 lg:self-start">
          <BookingWidget property={property} onContactHost={async () => {
            if (!user) return navigate('/login');
            try {
              toast.info('Opening chat with host…');
              navigate('/messages');
            } catch {
              toast.error('Could not start chat');
            }
          }} />
        </aside>
      </div>
    </div>
  );
}

function ScoreBar({ label, value }: { label: string; value: number }) {
  const pct = Math.max(0, Math.min(100, value));
  return (
    <div>
      <div className="flex justify-between text-xs font-semibold">
        <span className="text-muted">{label}</span>
        <span>{Math.round(pct)}</span>
      </div>
      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-line">
        <div className="h-full rounded-full bg-brand-600" style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function BookingWidget({ property, onContactHost }: { property: import('@/types/api').Property; onContactHost: () => void }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const toast = useToast();
  const [checkIn, setCheckIn] = useState('');
  const [checkOut, setCheckOut] = useState('');

  const price = priceLabel(property);
  const nightCount = checkIn && checkOut ? nights(checkIn, checkOut) : 0;
  const isNightly = property.stayType === StayType.ShortTerm && !!property.dailyRate;
  const subtotal = isNightly ? (property.dailyRate ?? 0) * nightCount : property.monthlyRent;
  const serviceFee = Math.round(subtotal * 0.05);
  const total = subtotal + serviceFee;

  const reserve = useMutation({
    mutationFn: async () => {
      const booking = await bookingsApi.create({ propertyId: property.propertyId, checkInDate: checkIn, checkOutDate: checkOut });
      const escrow = await escrowApi.initiate(booking.bookingId);
      return { booking, escrow };
    },
    onSuccess: ({ escrow }) => {
      if (escrow.checkoutUrl) {
        toast.info('Redirecting to secure escrow payment…');
        window.location.href = escrow.checkoutUrl;
      } else {
        toast.success('Booking created — payment is held in escrow.');
        navigate('/dashboard/trips');
      }
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : 'Could not reserve'),
  });

  function onReserve() {
    if (!user) return navigate('/login', { state: { from: `/property/${property.propertyId}` } });
    if (!checkIn || !checkOut || nightCount < 1) return toast.error('Pick your dates first');
    reserve.mutate();
  }

  return (
    <div className="card p-5 shadow-soft">
      <div className="flex items-baseline justify-between">
        <p className="text-xl font-extrabold">
          {money(price.amount)} <span className="text-sm font-medium text-muted">{price.unit}</span>
        </p>
        <span className="pill bg-brand-50 text-brand-700"><Shield className="h-3.5 w-3.5" /> Escrow-protected</span>
      </div>

      <div className="mt-4 grid grid-cols-2 overflow-hidden rounded-xl border border-line">
        <label className="border-r border-line p-3">
          <span className="block text-[11px] font-bold uppercase text-muted">Check-in</span>
          <input type="date" value={checkIn} onChange={(e) => setCheckIn(e.target.value)} className="w-full bg-transparent text-sm font-semibold outline-none" />
        </label>
        <label className="p-3">
          <span className="block text-[11px] font-bold uppercase text-muted">Check-out</span>
          <input type="date" value={checkOut} min={checkIn} onChange={(e) => setCheckOut(e.target.value)} className="w-full bg-transparent text-sm font-semibold outline-none" />
        </label>
      </div>

      <Button block className="mt-4" loading={reserve.isPending} onClick={onReserve}>
        Reserve
      </Button>
      <p className="mt-2 text-center text-xs text-muted">You won't be charged until the booking is confirmed.</p>

      {(isNightly ? nightCount > 0 : true) && (
        <div className="mt-4 space-y-2 border-t border-line pt-4 text-sm">
          <Row l={isNightly ? `${money(property.dailyRate ?? 0)} × ${nightCount} nights` : 'Monthly rent'} r={money(subtotal)} />
          <Row l="TripNest service fee" r={money(serviceFee)} />
          <div className="flex justify-between border-t border-line pt-2 font-bold">
            <span>Total</span>
            <span>{money(total)}</span>
          </div>
        </div>
      )}

      <button onClick={onContactHost} className="mt-4 flex w-full items-center justify-center gap-2 rounded-full border border-line py-2.5 text-sm font-bold hover:border-ink">
        <ChatIcon className="h-4 w-4" /> Contact host
      </button>

      <div className="mt-4 flex items-start gap-2 rounded-lg bg-surface p-3 text-xs text-muted">
        <Camera className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
        Every TripNest listing is reviewed via host walkthrough video before going live.
      </div>
    </div>
  );
}

function Row({ l, r }: { l: string; r: string }) {
  return (
    <div className="flex justify-between text-muted">
      <span>{l}</span>
      <span className="text-ink">{r}</span>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="container-tn py-6">
      <Skeleton className="h-8 w-1/2" />
      <Skeleton className="mt-4 h-[420px] w-full rounded-2xl" />
      <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_380px]">
        <div className="space-y-4">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
        <Skeleton className="h-80 w-full rounded-xl" />
      </div>
    </div>
  );
}
