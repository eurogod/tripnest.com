import { getSavedProperties } from '../../api/properties';
import { useAsync } from '../../hooks/useAsync';
import { useSavedIds } from '../../store/savedStore';
import { useT } from '../../lib/i18n';
import AsyncBoundary from '../../components/AsyncBoundary';
import PropertyCard from '../../components/tenant/PropertyCard';

export default function SavedPage() {
  const state = useAsync(getSavedProperties, []);
  // Filtering against the live wishlist set means unsaving a card (here or
  // anywhere else) removes it immediately — and the removal persists.
  const savedIds = useSavedIds();
  const t = useT();

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold text-ink">{t('Saved Listings')}</h1>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading saved listings…"
        errorMessage="Failed to load saved listings."
      >
        {(saved) => {
          const items = savedIds ? saved.filter((p) => savedIds.has(p.id)) : saved;
          return items.length === 0 ? (
            <p className="text-muted">{t('You haven’t saved any listings yet.')}</p>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {items.map((p) => (
                <PropertyCard key={p.id} property={p} initialSaved />
              ))}
            </div>
          );
        }}
      </AsyncBoundary>
    </div>
  );
}
