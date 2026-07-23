import { useMemo, useState } from 'react';
import { getProperties } from '../../api/properties';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import PropertyCard from '../../components/tenant/PropertyCard';
import { useT } from '../../lib/i18n';
import { SearchIcon } from '../../components/tenant/icons';

const CATEGORIES = ['All', 'Apartments', 'Student Rooms', 'Short Stay'];
type Sort = 'recommended' | 'price-asc' | 'price-desc' | 'rating';

export default function SearchPage() {
  const state = useAsync(getProperties, []);
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('All');
  const [sort, setSort] = useState<Sort>('recommended');
  const t = useT();

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    let list = (state.data ?? []).filter(
      (p) =>
        (category === 'All' || p.type === category) &&
        (q === '' || p.title.toLowerCase().includes(q) || p.location.toLowerCase().includes(q)),
    );
    if (sort === 'price-asc') list = [...list].sort((a, b) => a.price - b.price);
    else if (sort === 'price-desc') list = [...list].sort((a, b) => b.price - a.price);
    else if (sort === 'rating') list = [...list].sort((a, b) => b.rating - a.rating);
    return list;
  }, [state.data, query, category, sort]);

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold text-ink">Search Properties</h1>

      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted">
            <SearchIcon size={18} />
          </span>
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t('Search by title or location…')}
            className="w-full rounded-full border border-gray-200 bg-white py-2.5 pl-10 pr-4 text-sm text-ink outline-none focus:border-brand"
          />
        </div>
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value as Sort)}
          className="rounded-full border border-gray-200 bg-white px-4 py-2.5 text-sm text-ink outline-none focus:border-brand"
        >
          <option value="recommended">{t('Recommended')}</option>
          <option value="price-asc">{t('Price: Low to High')}</option>
          <option value="price-desc">{t('Price: High to Low')}</option>
          <option value="rating">{t('Top rated')}</option>
        </select>
      </div>

      <div className="mb-6 flex flex-wrap gap-2">
        {CATEGORIES.map((c) => (
          <button
            key={c}
            onClick={() => setCategory(c)}
            className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
              category === c
                ? 'border-brand bg-brand-50 text-brand'
                : 'border-gray-200 text-gray-600 hover:bg-gray-100'
            }`}
          >
            {t(c)}
          </button>
        ))}
      </div>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading properties…"
        errorMessage="Failed to load properties."
      >
        {() => (
          <>
            <p className="mb-4 text-sm text-muted">
              {results.length} {results.length === 1 ? 'property' : 'properties'} found
            </p>
            {results.length === 0 ? (
              <p className="text-muted">No properties match your search.</p>
            ) : (
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {results.map((p) => (
                  <PropertyCard key={p.id} property={p} />
                ))}
              </div>
            )}
          </>
        )}
      </AsyncBoundary>
    </div>
  );
}
