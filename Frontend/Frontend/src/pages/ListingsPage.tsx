import { useState } from 'react';
import type { Listing, ListingStatus } from '../types';
import { getListings } from '../api/listings';
import { useAsync } from '../hooks/useAsync';
import AddListingModal from '../components/landlord/AddListingModal';
import EditListingModal from '../components/landlord/EditListingModal';
import WalkthroughManagerModal from '../components/landlord/WalkthroughManagerModal';
import TourEditorModal from '../components/landlord/TourEditorModal';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import Badge, { type BadgeTone } from '../components/ui/Badge';
import { formatCurrency } from '../lib/format';

const STATUS_TONE: Record<ListingStatus, BadgeTone> = {
  published: 'green',
  unlisted: 'gray',
  draft: 'amber',
  snoozed: 'blue',
};

const STATUS_LABEL: Record<ListingStatus, string> = {
  published: 'Published',
  unlisted: 'Unlisted',
  draft: 'Draft',
  snoozed: 'Snoozed',
};

function ListingCard({ listing, onUpdated }: {
  listing: Listing;
  onUpdated: (listing: Listing) => void;
}) {
  const [editOpen, setEditOpen] = useState(false);
  const [managerOpen, setManagerOpen] = useState(false);
  const [tourOpen, setTourOpen] = useState(false);

  return (
    <Card className="overflow-hidden">
      <div className="h-32 w-full" style={{ backgroundColor: listing.coverColor }} />
      <div className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-semibold text-ink">{listing.title}</p>
            <p className="text-sm text-muted">{listing.location}</p>
          </div>
          <Badge tone={STATUS_TONE[listing.status]}>{STATUS_LABEL[listing.status]}</Badge>
        </div>

        <div className="mt-4 flex items-center gap-4 text-sm text-muted">
          <span>{listing.type}</span>
          <span>·</span>
          <span>{listing.beds} bd</span>
          <span>·</span>
          <span>{listing.baths} ba</span>
        </div>

        <div className="mt-4 flex items-center justify-between">
          <span className="font-semibold text-ink">
            {formatCurrency(listing.nightlyRate)}
            <span className="font-normal text-muted"> / night</span>
          </span>
          {listing.reviews > 0 && (
            <span className="text-sm text-muted">
              ★ {listing.rating} ({listing.reviews})
            </span>
          )}
        </div>

        <p className="mt-2 text-xs text-muted">{listing.occupancyRate}% occupancy</p>

        <div className="mt-4 flex gap-2">
          <Button variant="ghost" size="sm" onClick={() => setEditOpen(true)}>Edit</Button>
          <Button variant="ghost" size="sm" onClick={() => setManagerOpen(true)}>Videos</Button>
          <Button variant="ghost" size="sm" onClick={() => setTourOpen(true)}>Tour</Button>
        </div>
        {listing.status !== 'published' && (
          <p className="mt-2 text-xs text-muted">
            Goes live once TripNest verifies the listing.
          </p>
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

  const replace = (updated: Listing) =>
    setListings((ls) => ls.map((l) => (l.id === updated.id ? updated : l)));

  return (
    <>
      <div className="mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-4xl font-bold text-ink">Listings</h1>
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

export default function ListingsPage() {
  const state = useAsync(getListings, []);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading listings…"
      errorMessage="Failed to load listings."
      emptyMessage="No listings yet."
      isEmpty={(rows) => rows.length === 0}
    >
      {(rows) => <ListingsView initial={rows} />}
    </AsyncBoundary>
  );
}
