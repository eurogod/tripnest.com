import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { Listing } from '../../types';
import { getListingById, setListingCover } from '../../api/listings';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import {
  ChevronLeftIcon, ChevronRightIcon, SearchIcon, MapPinIcon, CheckIcon, StarIcon,
} from '../../components/tenant/icons';
import type { ListingStatus } from '../../types';

const STATUS: Record<ListingStatus, { tone: BadgeTone; label: string }> = {
  published: { tone: 'green', label: 'Published' },
  unlisted: { tone: 'gray', label: 'Offline' },
  draft: { tone: 'amber', label: 'Draft' },
  snoozed: { tone: 'blue', label: 'Archived' },
};

function Gallery({ initial }: { initial: Listing }) {
  const navigate = useNavigate();
  const [listing, setListing] = useState(initial);
  const [index, setIndex] = useState(0);
  const [zoomed, setZoomed] = useState(false);
  const [settingCover, setSettingCover] = useState(false);

  const photos = listing.photos;
  const hasPhotos = photos.length > 0;
  const current = photos[index];

  const go = (delta: number) => {
    if (!hasPhotos) return;
    setIndex((i) => (i + delta + photos.length) % photos.length);
  };

  // Arrow-key navigation and Esc to exit zoom.
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

  const makeCover = async () => {
    if (!current || current.isCover) return;
    setSettingCover(true);
    try {
      setListing(await setListingCover(listing.id, current.id));
      setIndex(0); // cover moves to the front
    } catch {
      /* leave as-is; the badge simply won't change */
    } finally {
      setSettingCover(false);
    }
  };

  const status = STATUS[listing.status];

  return (
    <div className="mx-auto max-w-4xl">
      <button
        type="button"
        onClick={() => navigate('/landlord/listings')}
        className="mb-4 flex items-center gap-1.5 text-sm font-semibold text-muted transition-colors hover:text-ink"
      >
        <ChevronLeftIcon size={16} /> Back to listings
      </button>

      {/* Main viewer */}
      <div className="relative overflow-hidden rounded-2xl bg-gray-100">
        {hasPhotos ? (
          <div className="relative aspect-[16/10] w-full">
            <img src={current.url} alt={listing.title} className="h-full w-full object-cover" />
            {current.isCover && (
              <Badge tone="green" className="absolute left-3 top-3">Cover</Badge>
            )}
            <button
              type="button"
              onClick={() => setZoomed(true)}
              aria-label="Zoom"
              className="absolute right-3 top-3 flex h-10 w-10 items-center justify-center rounded-full bg-black/50 text-white transition-colors hover:bg-black/70"
            >
              <SearchIcon size={18} />
            </button>
            {photos.length > 1 && (
              <>
                <button
                  type="button"
                  onClick={() => go(-1)}
                  aria-label="Previous photo"
                  className="absolute left-3 top-1/2 flex h-11 w-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-ink shadow transition-colors hover:bg-white"
                >
                  <ChevronLeftIcon size={20} />
                </button>
                <button
                  type="button"
                  onClick={() => go(1)}
                  aria-label="Next photo"
                  className="absolute right-3 top-1/2 flex h-11 w-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-ink shadow transition-colors hover:bg-white"
                >
                  <ChevronRightIcon size={20} />
                </button>
                <div className="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-black/55 px-3 py-1 text-xs font-medium text-white">
                  {index + 1} / {photos.length}
                </div>
              </>
            )}
          </div>
        ) : (
          <div
            className="flex aspect-[16/10] w-full items-center justify-center"
            style={{ backgroundColor: listing.coverColor }}
          >
            <p className="text-sm font-medium text-white/80">No photos uploaded yet</p>
          </div>
        )}
      </div>

      {/* Thumbnails */}
      {photos.length > 1 && (
        <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
          {photos.map((p, i) => (
            <button
              key={p.id}
              type="button"
              onClick={() => setIndex(i)}
              className={`relative h-16 w-24 shrink-0 overflow-hidden rounded-lg border-2 transition-colors ${
                i === index ? 'border-brand' : 'border-transparent hover:border-gray-300'
              }`}
            >
              <img src={p.url} alt="" className="h-full w-full object-cover" />
              {p.isCover && (
                <span className="absolute inset-x-0 bottom-0 bg-brand/90 py-0.5 text-center text-[10px] font-semibold text-white">
                  Cover
                </span>
              )}
            </button>
          ))}
        </div>
      )}

      {hasPhotos && current && !current.isCover && (
        <div className="mt-3">
          <Button variant="ghost" size="sm" onClick={() => void makeCover()} disabled={settingCover}>
            <span className="flex items-center gap-1.5">
              <CheckIcon size={15} /> {settingCover ? 'Setting cover…' : 'Set as cover photo'}
            </span>
          </Button>
        </div>
      )}

      {/* Full details */}
      <Card className="mt-5 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold text-ink">{listing.title}</h1>
            <p className="mt-1 flex items-center gap-1 text-sm text-muted">
              <MapPinIcon size={14} /> {listing.location}
            </p>
          </div>
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>

        <div className="mt-4 flex flex-wrap items-end gap-x-6 gap-y-2">
          <p className="text-2xl font-bold text-brand">
            {formatCedi(listing.nightlyRate)}
            <span className="text-sm font-normal text-muted"> / night</span>
          </p>
          <p className="text-sm text-muted">{listing.beds} bd · {listing.baths} ba · {listing.type}</p>
          {listing.reviews > 0 && (
            <span className="flex items-center gap-1 text-sm text-ink">
              <StarIcon size={13} className="text-amber-400" /> {listing.rating} ({listing.reviews})
            </span>
          )}
        </div>

        {listing.description && (
          <div className="mt-5">
            <h2 className="mb-1.5 text-sm font-semibold text-ink">About this place</h2>
            <p className="whitespace-pre-line text-sm leading-relaxed text-gray-600">{listing.description}</p>
          </div>
        )}

        {listing.amenities.length > 0 && (
          <div className="mt-5">
            <h2 className="mb-2 text-sm font-semibold text-ink">Amenities</h2>
            <div className="flex flex-wrap gap-2">
              {listing.amenities.map((a) => (
                <span key={a} className="flex items-center gap-1.5 rounded-lg bg-gray-100 px-3 py-1.5 text-sm text-gray-700">
                  <CheckIcon size={14} className="text-brand" /> {a}
                </span>
              ))}
            </div>
          </div>
        )}
      </Card>

      {/* Fullscreen zoom */}
      {zoomed && current && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Zoomed photo"
          className="fixed inset-0 z-[100] flex items-center justify-center bg-black/95 p-4"
          onClick={() => setZoomed(false)}
        >
          <img src={current.url} alt={listing.title} className="max-h-full max-w-full object-contain" />
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

export default function ListingGalleryPage() {
  const { id } = useParams();
  const state = useAsync(() => getListingById(id ?? ''), [id]);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading listing…"
      errorMessage="Couldn't load this listing."
    >
      {(listing) => <Gallery initial={listing} />}
    </AsyncBoundary>
  );
}
