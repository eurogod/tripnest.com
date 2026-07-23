import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Listing } from '../../types';
import { getListings, setListingStatus } from '../../api/listings';
import { useAsync } from '../../hooks/useAsync';
import AddListingModal from '../../components/landlord/AddListingModal';
import EditListingModal from '../../components/landlord/EditListingModal';
import WalkthroughManagerModal from '../../components/landlord/WalkthroughManagerModal';
import TourEditorModal from '../../components/landlord/TourEditorModal';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import { formatCedi } from '../../lib/format';
import { StarIcon, MapPinIcon } from '../../components/tenant/icons';

/**
 * Publish / take-offline control. A listing is a private Draft until the host
 * publishes it (goes live instantly — no admin approval); once published, a red
 * "GoOffline" sits next to the "Published" state to pull it back down.
 */
function StatusControl({ listing, onUpdated }: {
  listing: Listing;
  onUpdated: (listing: Listing) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const published = listing.status === 'published';

  const change = async (status: 'active' | 'inactive') => {
    setBusy(true);
    setError(null);
    try {
      onUpdated(await setListingStatus(listing.id, status));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not update. Try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="flex items-center gap-2">
        {published ? (
          <>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-50 px-3 py-1 text-xs font-semibold text-brand">
              <span className="h-2 w-2 rounded-full bg-brand" /> Published
            </span>
            <button
              type="button"
              onClick={() => void change('inactive')}
              disabled={busy}
              className="rounded-full bg-rose-600 px-3 py-1 text-xs font-semibold text-white transition-colors hover:bg-rose-700 disabled:opacity-50"
            >
              {busy ? 'Working…' : 'GoOffline'}
            </button>
          </>
        ) : (
          <>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-3 py-1 text-xs font-semibold text-gray-500">
              <span className="h-2 w-2 rounded-full bg-gray-400" /> Draft
            </span>
            <button
              type="button"
              onClick={() => void change('active')}
              disabled={busy}
              className="rounded-full bg-brand px-3 py-1 text-xs font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-50"
            >
              {busy ? 'Working…' : 'Publish'}
            </button>
          </>
        )}
      </div>
      {error && <p className="mt-1 text-xs text-rose-600">{error}</p>}
    </div>
  );
}

function ListingCard({ listing, onUpdated }: {
  listing: Listing;
  onUpdated: (listing: Listing) => void;
}) {
  const navigate = useNavigate();
  const [editOpen, setEditOpen] = useState(false);
  const [managerOpen, setManagerOpen] = useState(false);
  const [tourOpen, setTourOpen] = useState(false);

  return (
    <Card className="overflow-hidden">
      <button
        type="button"
        onClick={() => navigate(`/landlord/listings/${listing.id}`)}
        aria-label={`View ${listing.title} photos and details`}
        className="group relative block h-32 w-full overflow-hidden"
        style={{ backgroundColor: listing.coverPhoto ? undefined : listing.coverColor }}
      >
        {listing.coverPhoto ? (
          <img
            src={listing.coverPhoto}
            alt={listing.title}
            className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
          />
        ) : (
          <span className="flex h-full w-full items-center justify-center text-xs font-medium text-white/80">
            Add photos
          </span>
        )}
        <span className="absolute inset-x-0 bottom-0 flex items-center justify-center gap-1 bg-black/45 py-1 text-xs font-medium text-white opacity-0 transition-opacity group-hover:opacity-100">
          View photos & details
        </span>
      </button>
      <div className="p-4">
        <h3 className="font-semibold text-ink">{listing.title}</h3>
        <p className="flex items-center gap-1 text-sm text-muted">
          <MapPinIcon size={13} /> {listing.location}
        </p>
        <div className="mt-3 flex items-end justify-between">
          <p className="font-bold text-brand">
            {formatCedi(listing.nightlyRate)}<span className="text-xs font-normal text-muted"> / night</span>
          </p>
          {listing.reviews > 0 && (
            <span className="flex items-center gap-1 text-xs text-ink">
              <StarIcon size={12} className="text-amber-400" /> {listing.rating} ({listing.reviews})
            </span>
          )}
        </div>
        <div className="mt-3">
          <StatusControl listing={listing} onUpdated={onUpdated} />
        </div>
        <div className="mt-3 flex gap-2">
          {/* A published listing is locked for editing — take it offline first. */}
          {listing.status === 'published' ? (
            <Button variant="ghost" size="sm" onClick={() => navigate(`/landlord/listings/${listing.id}`)}>View</Button>
          ) : (
            <Button variant="ghost" size="sm" onClick={() => setEditOpen(true)}>Edit</Button>
          )}
          <Button variant="ghost" size="sm" onClick={() => setManagerOpen(true)}>Videos</Button>
          <Button variant="ghost" size="sm" onClick={() => setTourOpen(true)}>Tour</Button>
        </div>
        {listing.status === 'published' && (
          <p className="mt-2 text-xs text-muted">Take the listing offline to edit its details.</p>
        )}
      </div>
      {editOpen && (
        <EditListingModal
          listingId={listing.id}
          onClose={() => setEditOpen(false)}
          onUpdated={onUpdated}
        />
      )}
      {managerOpen && (
        <WalkthroughManagerModal
          listingId={listing.id}
          listingTitle={listing.title}
          onClose={() => setManagerOpen(false)}
        />
      )}
      {tourOpen && (
        <TourEditorModal
          listingId={listing.id}
          listingTitle={listing.title}
          onClose={() => setTourOpen(false)}
        />
      )}
    </Card>
  );
}

function ListingsView({ initial }: { initial: Listing[] }) {
  const [listings, setListings] = useState(initial);
  const published = listings.filter((l) => l.status === 'published').length;
  const avgOccupancy = listings.length
    ? Math.round(listings.reduce((s, l) => s + l.occupancyRate, 0) / listings.length)
    : 0;

  const replace = (updated: Listing) =>
    setListings((ls) => ls.map((l) => (l.id === updated.id ? updated : l)));

  return (
    <>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-ink">My Listings</h1>
          <p className="mt-1 text-sm text-muted">{published} published · {listings.length} total · {avgOccupancy}% avg occupancy</p>
        </div>
        <AddListingModal onCreated={(l) => setListings((ls) => [l, ...ls])} />
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {listings.map((l) => (
          <ListingCard key={l.id} listing={l} onUpdated={replace} />
        ))}
      </div>
    </>
  );
}

export default function LandlordListingsPage() {
  const state = useAsync(getListings, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading listings…" errorMessage="Failed to load listings." emptyMessage="No listings yet." isEmpty={(r) => r.length === 0}>
      {(rows) => <ListingsView initial={rows} />}
    </AsyncBoundary>
  );
}
