import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import type { Property } from '../../types';
import { getPropertyById } from '../../api/properties';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import TrustScoreBadge from '../../components/tenant/TrustScoreBadge';
import Avatar from '../../components/ui/Avatar';
import BookingWidget from '../../components/tenant/BookingWidget';
import { useSavedIds, toggleSaved } from '../../store/savedStore';
import {
  AmenityIcon, ShieldIcon, StarIcon, MapPinIcon, ChevronLeftIcon, ChevronRightIcon,
} from '../../components/tenant/icons';

// Loaded on demand — see PropertyCard for the same pattern.
const VirtualTour = lazy(() => import('../../components/tenant/tour/VirtualTour'));

/**
 * Published-photo gallery: the cover leads, prev/next steps through the rest,
 * and a double-click (or the zoom button) opens the current photo fullscreen.
 * Falls back to a gradient + tour prompt when a listing has no photos.
 */
function Gallery({ photos, title, onPlay }: { photos: string[]; title: string; onPlay: () => void }) {
  const [index, setIndex] = useState(0);
  const [zoomed, setZoomed] = useState(false);
  const has = photos.length > 0;
  const current = photos[index];

  const go = (delta: number) => {
    if (!has) return;
    setIndex((i) => (i + delta + photos.length) % photos.length);
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight') go(1);
      else if (e.key === 'ArrowLeft') go(-1);
      else if (e.key === 'Escape') setZoomed(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [photos.length]);

  if (!has) {
    return (
      <button
        onClick={onPlay}
        aria-label="Start virtual tour"
        className="group relative flex h-56 w-full items-center justify-center overflow-hidden rounded-xl bg-gradient-to-br from-brand-50 to-gray-200 sm:h-72"
      >
        <span className="flex items-center gap-2 rounded-full bg-white/95 px-4 py-2 text-sm font-semibold text-ink shadow">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false"><path d="M8 5v14l11-7z" /></svg>
          Start virtual tour
        </span>
      </button>
    );
  }

  return (
    <div>
      <div className="relative overflow-hidden rounded-xl bg-gray-100">
        <img
          src={current}
          alt={title}
          onDoubleClick={() => setZoomed(true)}
          className="h-56 w-full cursor-zoom-in object-cover sm:h-80"
        />
        <button
          type="button"
          onClick={() => setZoomed(true)}
          aria-label="Zoom photo"
          className="absolute right-3 top-3 flex h-10 w-10 items-center justify-center rounded-full bg-black/50 text-white transition-colors hover:bg-black/70"
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" /><line x1="11" y1="8" x2="11" y2="14" /><line x1="8" y1="11" x2="14" y2="11" /></svg>
        </button>
        <button
          type="button"
          onClick={onPlay}
          className="absolute bottom-3 right-3 flex items-center gap-1.5 rounded-full bg-white/90 px-3 py-1.5 text-xs font-semibold text-ink shadow transition-colors hover:bg-white"
        >
          <svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z" /></svg>
          Virtual tour
        </button>
        {photos.length > 1 && (
          <>
            <button
              type="button"
              onClick={() => go(-1)}
              aria-label="Previous photo"
              className="absolute left-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-ink shadow transition-colors hover:bg-white"
            >
              <ChevronLeftIcon size={20} />
            </button>
            <button
              type="button"
              onClick={() => go(1)}
              aria-label="Next photo"
              className="absolute right-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-ink shadow transition-colors hover:bg-white"
            >
              <ChevronRightIcon size={20} />
            </button>
            <div className="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-black/55 px-3 py-1 text-xs font-medium text-white">
              {index + 1} / {photos.length}
            </div>
          </>
        )}
      </div>

      {photos.length > 1 && (
        <div className="mt-2 flex gap-2 overflow-x-auto pb-1">
          {photos.map((url, i) => (
            <button
              key={url}
              type="button"
              onClick={() => setIndex(i)}
              aria-label={`Photo ${i + 1}`}
              className={`h-16 w-24 shrink-0 overflow-hidden rounded-lg border-2 transition-colors ${
                i === index ? 'border-brand' : 'border-transparent hover:border-gray-300'
              }`}
            >
              <img src={url} alt="" className="h-full w-full object-cover" />
            </button>
          ))}
        </div>
      )}

      {zoomed && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Zoomed photo"
          className="fixed inset-0 z-[100] flex items-center justify-center bg-black/95 p-4"
          onClick={() => setZoomed(false)}
        >
          <img src={current} alt={title} className="max-h-full max-w-full object-contain" />
          <button
            type="button"
            onClick={() => setZoomed(false)}
            aria-label="Close zoom"
            className="absolute right-5 top-5 flex h-11 w-11 items-center justify-center rounded-full bg-white/15 text-2xl leading-none text-white hover:bg-white/25"
          >
            ×
          </button>
          {photos.length > 1 && (
            <>
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); go(-1); }}
                aria-label="Previous photo"
                className="absolute left-5 top-1/2 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
              >
                <ChevronLeftIcon size={24} />
              </button>
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); go(1); }}
                aria-label="Next photo"
                className="absolute right-5 top-1/2 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
              >
                <ChevronRightIcon size={24} />
              </button>
            </>
          )}
        </div>
      )}
    </div>
  );
}

function Detail({ property }: { property: Property }) {
  const [tourOpen, setTourOpen] = useState(false);
  const savedIds = useSavedIds();
  const saved = savedIds?.has(property.id) ?? false;

  return (
    <div>
      <Link to="/search" className="text-sm font-semibold text-brand no-underline">
        ← Back to search
      </Link>

      <div className="mt-4 grid grid-cols-1 gap-8 lg:grid-cols-[1fr_340px]">
        <div className="min-w-0 space-y-6">
          <Gallery photos={property.photos ?? []} title={property.title} onPlay={() => setTourOpen(true)} />

          <div>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-2xl font-bold text-ink">{property.title}</h1>
              <TrustScoreBadge propertyId={property.id} />
              {property.verified && (
                <Badge tone="green">
                  <span className="inline-flex items-center gap-1">
                    <ShieldIcon size={12} /> Verified
                  </span>
                </Badge>
              )}
              <button
                aria-label={saved ? 'Unsave property' : 'Save property'}
                aria-pressed={saved}
                onClick={() => void toggleSaved(property.id)}
                className={`ml-auto flex h-9 w-9 items-center justify-center rounded-full border border-gray-200 bg-white ${
                  saved ? 'text-rose-500' : 'text-gray-500 hover:text-rose-500'
                }`}
              >
                <svg width={17} height={17} viewBox="0 0 24 24" fill={saved ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
                  <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                </svg>
              </button>
            </div>
            <p className="mt-1 flex items-center gap-1.5 text-muted">
              <MapPinIcon size={15} /> {property.location}
              <span className="mx-1">·</span>
              <StarIcon size={13} className="text-amber-400" />
              <span className="font-semibold text-ink">{property.rating}</span>
              <span>({property.reviews} reviews)</span>
            </p>
            <p className="mt-1 text-xs text-muted">TN-ID: {property.id}</p>
          </div>

          <div className="flex flex-wrap gap-4 border-y border-gray-100 py-4 text-sm text-ink">
            <span>{property.type}</span>
            <span>· {property.beds} bed{property.beds > 1 ? 's' : ''}</span>
            <span>· {property.baths} bath{property.baths > 1 ? 's' : ''}</span>
          </div>

          <div>
            <h2 className="mb-2 text-lg font-bold text-ink">About this place</h2>
            <p className="text-muted">{property.description}</p>
          </div>

          <div>
            <h2 className="mb-3 text-lg font-bold text-ink">Amenities</h2>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
              {property.amenities.map((a) => (
                <span key={a} className="flex items-center gap-2 text-sm text-ink">
                  <span className="text-brand"><AmenityIcon name={a} size={16} /></span> {a}
                </span>
              ))}
            </div>
          </div>

          <Card className="flex items-center gap-3 p-4">
            <Avatar name={property.agent.name} size={44} />
            <div className="min-w-0 flex-1">
              <p className="font-semibold text-ink">{property.agent.name}</p>
              <p className="text-sm text-muted">{property.agent.role}</p>
            </div>
            <Button variant="ghost" size="sm">Contact</Button>
          </Card>
        </div>

        <aside className="min-w-0">
          <BookingWidget property={property} />
        </aside>
      </div>

      {tourOpen && (
        <Suspense fallback={null}>
          <VirtualTour propertyId={property.id} onClose={() => setTourOpen(false)} />
        </Suspense>
      )}
    </div>
  );
}

export default function PropertyDetailPage() {
  const { id = '' } = useParams();
  const state = useAsync(() => getPropertyById(id), [id]);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading property…"
      errorMessage="Failed to load property."
    >
      {(property) =>
        property ? (
          <Detail property={property} />
        ) : (
          <div>
            <p className="text-muted">Property not found.</p>
            <Link to="/search" className="text-sm font-semibold text-brand no-underline">
              ← Back to search
            </Link>
          </div>
        )
      }
    </AsyncBoundary>
  );
}
