import { getProviders } from '../../api/services';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import ProviderCard from '../../components/tenant/ProviderCard';

interface ServiceDirectoryProps {
  category: string;
  title: string;
  subtitle: string;
}

/** Shared directory listing for a "Services" category (Caretakers, etc.). */
export default function ServiceDirectory({ category, title, subtitle }: ServiceDirectoryProps) {
  const state = useAsync(() => getProviders(category), [category]);

  return (
    <div>
      <h1 className="text-3xl font-bold text-ink">{title}</h1>
      <p className="mb-6 mt-1 text-muted">{subtitle}</p>

      <AsyncBoundary
        state={state}
        loadingMessage={`Loading ${title.toLowerCase()}…`}
        errorMessage={`Failed to load ${title.toLowerCase()}.`}
        emptyMessage={`No ${title.toLowerCase()} available yet.`}
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {rows.map((p) => (
              <ProviderCard key={p.id} provider={p} />
            ))}
          </div>
        )}
      </AsyncBoundary>
    </div>
  );
}
