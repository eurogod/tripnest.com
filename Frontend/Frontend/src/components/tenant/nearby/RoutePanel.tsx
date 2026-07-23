import type { Property } from '../../../types';
import type { RoutesResponse } from '../../../lib/routing';
import { formatDistance, formatDuration, etaClock } from '../../../lib/geo';
import { type Poi, POI_META, type PoiCategory } from '../../../lib/poi';
import Button from '../../ui/Button';
import { ChevronRightIcon } from '../icons';

export type TravelMode = 'driving' | 'walking';

interface RoutePanelProps {
  property: Property;
  routes: RoutesResponse;
  mode: TravelMode;
  onMode: (m: TravelMode) => void;
  remainingKm: number;
  remainingMin: number;
  navigating: boolean;
  onToggleNav: () => void;
  onBack: () => void;
  pois: Poi[];
  poiLoading: boolean;
}

export default function RoutePanel({
  property, routes, mode, onMode, remainingKm, remainingMin, navigating, onToggleNav, onBack, pois, poiLoading,
}: RoutePanelProps) {
  const { primary, alternatives } = routes;
  const grouped = POI_GROUPS.map((c) => ({ cat: c, items: pois.filter((p) => p.category === c) }))
    .filter((g) => g.items.length > 0);

  return (
    <div className="space-y-4">
      <button onClick={onBack} className="text-sm font-semibold text-brand hover:underline">
        ← All apartments
      </button>

      <div>
        <h2 className="text-lg font-bold text-ink">{property.title}</h2>
        <p className="text-sm text-muted">{property.location}</p>
      </div>

      {/* Mode toggle */}
      <div className="flex rounded-lg border border-gray-200 p-1 text-sm font-medium">
        {(['driving', 'walking'] as TravelMode[]).map((m) => (
          <button
            key={m}
            onClick={() => onMode(m)}
            className={`flex-1 rounded-md py-1.5 capitalize transition-colors ${
              mode === m ? 'bg-brand-50 text-brand' : 'text-gray-600'
            }`}
          >
            {m === 'driving' ? '🚗 Driving' : '🚶 Walking'}
          </button>
        ))}
      </div>

      {/* Live ETA summary */}
      <div className="grid grid-cols-3 gap-2 rounded-xl bg-brand-50 p-3 text-center">
        <div>
          <p className="text-lg font-bold text-brand">{formatDuration(remainingMin)}</p>
          <p className="text-[11px] text-muted">ETA</p>
        </div>
        <div>
          <p className="text-lg font-bold text-ink">{formatDistance(remainingKm)}</p>
          <p className="text-[11px] text-muted">Remaining</p>
        </div>
        <div>
          <p className="text-lg font-bold text-ink">{etaClock(remainingMin)}</p>
          <p className="text-[11px] text-muted">Arrival</p>
        </div>
      </div>

      {primary.source === 'fallback' && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700">
          Showing a straight-line estimate — live road routing is unavailable right now.
        </p>
      )}

      <Button className="w-full" onClick={onToggleNav}>
        {navigating ? 'Stop navigation' : 'Start navigation'}
      </Button>

      {alternatives.length > 0 && (
        <p className="text-xs text-muted">
          {alternatives.length} alternative route{alternatives.length > 1 ? 's' : ''} shown on the map (dashed).
        </p>
      )}

      {/* Turn-by-turn */}
      <div>
        <h3 className="mb-2 text-sm font-bold text-ink">Directions</h3>
        <ol className="space-y-2">
          {primary.steps.map((s, i) => (
            <li key={i} className="flex items-start gap-3">
              <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-gray-100 text-xs font-semibold text-gray-600">
                {i + 1}
              </span>
              <span className="min-w-0 flex-1 text-sm text-ink">
                {s.instruction}
                {s.distanceM > 0 && (
                  <span className="block text-xs text-muted">{formatDistance(s.distanceM / 1000)}</span>
                )}
              </span>
            </li>
          ))}
        </ol>
      </div>

      {/* Nearby POIs */}
      <div>
        <h3 className="mb-2 text-sm font-bold text-ink">Around this apartment</h3>
        {poiLoading ? (
          <p className="text-sm text-muted">Finding nearby places…</p>
        ) : grouped.length === 0 ? (
          <p className="text-sm text-muted">No notable places found nearby.</p>
        ) : (
          <div className="space-y-3">
            {grouped.map(({ cat, items }) => (
              <div key={cat}>
                <p className="mb-1 flex items-center gap-1.5 text-xs font-semibold text-ink">
                  <span>{POI_META[cat].emoji}</span> {POI_META[cat].label}
                  <span className="text-muted">({items.length})</span>
                </p>
                <ul className="space-y-0.5 pl-5">
                  {items.slice(0, 4).map((p) => (
                    <li key={p.id} className="flex items-center gap-1 text-sm text-muted">
                      <ChevronRightIcon size={12} /> {p.name}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

const POI_GROUPS: PoiCategory[] = ['hospital', 'pharmacy', 'restaurant', 'shopping', 'school', 'bus_stop', 'fuel'];
