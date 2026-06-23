import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { propertiesApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { PropertyStatusPill } from '@/components/badges';
import { MapPin, Plus } from '@/components/icons';
import { money, priceLabel, propertyPhoto } from '@/lib/format';
import { useMyProperties } from '@/lib/hooks';
import { useToast } from '@/components/Toast';
import type { Property } from '@/types/api';

export default function HostListings() {
  const qc = useQueryClient();
  const toast = useToast();
  const query = useMyProperties();
  const [removing, setRemoving] = useState<Property | null>(null);
  const [busy, setBusy] = useState(false);

  async function confirmRemove() {
    if (!removing) return;
    setBusy(true);
    try {
      await propertiesApi.remove(removing.propertyId);
      toast.success('Listing removed');
      qc.invalidateQueries({ queryKey: ['my-properties'] });
      setRemoving(null);
    } catch {
      toast.error('Could not remove listing');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="My listings"
        subtitle="Manage the properties you host."
        action={
          <Link to="/host/listings/new">
            <Button>
              <Plus className="h-4 w-4" /> Add property
            </Button>
          </Link>
        }
      />
      <Async
        query={query}
        emptyIcon={<MapPin className="h-6 w-6" />}
        emptyTitle="No listings yet"
        emptySubtitle="Create your first listing to start hosting verified tenants."
        emptyAction={
          <Link to="/host/listings/new">
            <Button>Add property</Button>
          </Link>
        }
      >
        {(props) => (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {props.map((p) => {
              const price = priceLabel(p);
              return (
                <div key={p.propertyId} className="card overflow-hidden">
                  <Link to={`/property/${p.propertyId}`} className="block aspect-[4/3] overflow-hidden bg-line">
                    <img src={propertyPhoto(p)} alt={p.title} className="h-full w-full object-cover" />
                  </Link>
                  <div className="p-4">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="line-clamp-1 font-bold">{p.title}</h3>
                      <PropertyStatusPill status={p.status} />
                    </div>
                    <p className="mt-0.5 flex items-center gap-1 text-sm text-muted">
                      <MapPin className="h-3.5 w-3.5" /> {p.location}
                    </p>
                    <p className="mt-2 font-bold">
                      {money(price.amount)} <span className="text-sm font-medium text-muted">{price.unit}</span>
                    </p>
                    <div className="mt-3 flex gap-2">
                      <Link to={`/property/${p.propertyId}`} className="flex-1">
                        <Button variant="outline" size="sm" block>
                          View
                        </Button>
                      </Link>
                      <Button variant="ghost" size="sm" onClick={() => setRemoving(p)}>
                        Delete
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Async>

      <Modal open={!!removing} onClose={() => setRemoving(null)} title="Remove listing" maxWidth="max-w-sm">
        <p className="text-sm text-muted">
          Remove <span className="font-semibold text-ink">{removing?.title}</span>? This can’t be undone.
        </p>
        <div className="mt-5 flex gap-3">
          <Button variant="ghost" block onClick={() => setRemoving(null)}>
            Keep
          </Button>
          <Button variant="danger" block loading={busy} onClick={confirmRemove}>
            Remove
          </Button>
        </div>
      </Modal>
    </div>
  );
}
