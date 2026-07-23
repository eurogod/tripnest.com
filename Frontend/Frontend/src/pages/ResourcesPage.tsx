import { useState } from 'react';
import type { Resource, ResourceCategory } from '../types';
import { getResources } from '../api/resources';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Badge, { type BadgeTone } from '../components/ui/Badge';

const CATEGORY_TONE: Record<ResourceCategory, BadgeTone> = {
  guide: 'blue',
  policy: 'amber',
  template: 'green',
  video: 'red',
};

function ResourceCard({ resource }: { resource: Resource }) {
  return (
    <a
      href={resource.url}
      className="block no-underline"
    >
      <Card className="h-full p-5 transition-shadow hover:shadow-[0_1px_8px_rgba(0,0,0,0.08)]">
        <div className="flex items-center justify-between gap-2">
          <Badge tone={CATEGORY_TONE[resource.category]}>{resource.category}</Badge>
          <span className="text-xs text-muted">{resource.format}</span>
        </div>
        <h3 className="mt-3 font-semibold text-ink">{resource.title}</h3>
        <p className="mt-1 text-sm text-muted">{resource.description}</p>
      </Card>
    </a>
  );
}

export default function ResourcesPage() {
  const state = useAsync(getResources, []);
  const [query, setQuery] = useState('');

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">Resources</h1>

      <input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search guides, templates and policies…"
        className="mb-6 w-full max-w-md rounded-lg border border-gray-200 px-4 py-2.5 text-sm outline-none focus:border-brand"
      />

      <AsyncBoundary
        state={state}
        loadingMessage="Loading resources…"
        errorMessage="Failed to load resources."
      >
        {(rows) => {
          const q = query.trim().toLowerCase();
          const visible = q
            ? rows.filter(
                (r) =>
                  r.title.toLowerCase().includes(q) ||
                  r.description.toLowerCase().includes(q) ||
                  r.category.includes(q),
              )
            : rows;

          return visible.length === 0 ? (
            <p className="text-muted">
              {q ? `No resources match “${query}”.` : 'No resources have been published yet.'}
            </p>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {visible.map((r) => (
                <ResourceCard key={r.id} resource={r} />
              ))}
            </div>
          );
        }}
      </AsyncBoundary>
    </div>
  );
}
