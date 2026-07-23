import { Link } from 'react-router-dom';
import { getFeaturedProperties } from '../../../api/properties';
import { useAsync } from '../../../hooks/useAsync';
import AsyncBoundary from '../../AsyncBoundary';
import PropertyCard from '../PropertyCard';

export default function FeaturedProperties() {
  const state = useAsync(getFeaturedProperties, []);

  return (
    <section>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-ink">Featured Properties</h2>
        <Link to="/search" className="text-sm font-semibold text-brand no-underline">
          View all
        </Link>
      </div>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading properties…"
        errorMessage="Failed to load properties."
        emptyMessage="No properties available."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
            {rows.map((p) => (
              <PropertyCard key={p.id} property={p} />
            ))}
          </div>
        )}
      </AsyncBoundary>
    </section>
  );
}
