import { useEffect, useMemo, useRef, useState } from 'react';
import type { LatLng, Property } from '../../types';
import { getProperties } from '../../api/properties';
import { useGeolocation } from '../../hooks/useGeolocation';
import {
  haversineKm, formatDistance, formatDuration, drivingMinutes, walkingMinutes,
  distanceToPathM, remainingAlongPathKm,
} from '../../lib/geo';
import { getRoutes, type RoutesResponse } from '../../lib/routing';
import { getNearbyPois, type Poi } from '../../lib/poi';
import NearbyMap from '../../components/tenant/nearby/NearbyMap';
import RoutePanel, { type TravelMode } from '../../components/tenant/nearby/RoutePanel';
import { formatCedi } from '../../lib/format';
import Button from '../../components/ui/Button';
import { StarIcon, MapPinIcon, AmenityIcon } from '../../components/tenant/icons';

const FALLBACK_ORIGIN: LatLng = { lat: 5.3018, lng: -1.993 }; // Tarkwa centre
const DEVIATION_M = 50; // re-route if the user strays this far from the path
const REROUTE_COOLDOWN_MS = 6000;

interface AptRow {
  property: Property;
  distanceKm: number;
  drivingMin: number;
}

function StatusDot({ geo }: { geo: ReturnType<typeof useGeolocation> }) {
  const map = {
    granted: { c: 'bg-emerald-500', t: 'Live location' },
    locating: { c: 'bg-amber-500 animate-pulse', t: 'Locating…' },
    prompt: { c: 'bg-amber-500', t: 'Awaiting permission' },
    denied: { c: 'bg-rose-500', t: 'Location off' },
    unavailable: { c: 'bg-gray-400', t: 'Location unavailable' },
  }[geo.status];
  return (
    <span className="flex items-center gap-1.5 text-xs text-muted">
      <span className={`h-2 w-2 rounded-full ${map.c}`} /> {map.t}
      {geo.accuracy != null && geo.status === 'granted' && ` · ±${Math.round(geo.accuracy)}m`}
    </span>
  );
}

function ApartmentCard({ row, onSelect }: { row: AptRow; onSelect: () => void }) {
  const { property: p, distanceKm, drivingMin } = row;
  return (
    <button
      onClick={onSelect}
      className="flex w-full gap-3 rounded-xl border border-gray-200 bg-white p-3 text-left transition-shadow hover:shadow-md"
    >
      <div className="h-20 w-24 shrink-0 rounded-lg bg-gradient-to-br from-brand-50 to-gray-200" />
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <p className="truncate font-semibold text-ink">{p.title}</p>
          <span className="flex shrink-0 items-center gap-0.5 text-xs text-ink">
            <StarIcon size={12} className="text-amber-400" /> {p.rating}
          </span>
        </div>
        <p className="flex items-center gap-1 text-xs text-muted">
          <MapPinIcon size={11} /> {p.location}
        </p>
        <div className="mt-1 flex flex-wrap gap-x-2 gap-y-0.5">
          {p.amenities.slice(0, 3).map((a) => (
            <span key={a} className="flex items-center gap-0.5 text-[11px] text-muted">
              <AmenityIcon name={a} size={11} /> {a}
            </span>
          ))}
        </div>
        <div className="mt-1.5 flex items-center justify-between">
          <span className="text-sm font-bold text-brand">
            {formatCedi(p.price)}<span className="text-[11px] font-normal text-muted"> /{p.period}</span>
          </span>
          <span className="text-[11px] font-medium text-ink">
            {formatDistance(distanceKm)} · {formatDuration(drivingMin)} 🚗
          </span>
        </div>
      </div>
    </button>
  );
}

export default function NearbyPage() {
  const geo = useGeolocation();
  const [properties, setProperties] = useState<Property[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [routes, setRoutes] = useState<RoutesResponse | null>(null);
  const [routeLoading, setRouteLoading] = useState(false);
  const [mode, setMode] = useState<TravelMode>('driving');
  const [pois, setPois] = useState<Poi[]>([]);
  const [poiLoading, setPoiLoading] = useState(false);
  const [follow, setFollow] = useState(true);
  const [navigating, setNavigating] = useState(false);
  const lastRerouteRef = useRef(0);

  useEffect(() => {
    let alive = true;
    getProperties().then((p) => { if (alive) setProperties(p); });
    return () => { alive = false; };
  }, []);

  const origin = geo.position ?? FALLBACK_ORIGIN;
  const selected = properties.find((p) => p.id === selectedId) ?? null;

  // Sort apartments by proximity to the live location.
  const rows: AptRow[] = useMemo(() => {
    return properties
      .map((property) => {
        const distanceKm = haversineKm(origin, property.coords);
        return { property, distanceKm, drivingMin: drivingMinutes(distanceKm) };
      })
      .sort((a, b) => a.distanceKm - b.distanceKm)
      .slice(0, 60);
  }, [properties, origin]);

  // Fetch the road route, retaining loading state for the panel.
  const loadRoute = (from: LatLng, to: LatLng) => {
    setRouteLoading(true);
    getRoutes(from, to)
      .then((r) => setRoutes(r))
      .finally(() => setRouteLoading(false));
  };

  // Selecting an apartment (from the list or a map marker) drives all side effects
  // here — in a handler rather than an effect, so GPS ticks don't refetch the route.
  const select = (id: string) => {
    const property = properties.find((p) => p.id === id);
    if (!property) return;
    setSelectedId(id);
    setFollow(false);
    setNavigating(false);
    setPois([]);
    setPoiLoading(true);
    loadRoute(origin, property.coords);
    getNearbyPois(property.coords)
      .then(setPois)
      .finally(() => setPoiLoading(false));
  };

  const clearSelection = () => {
    setSelectedId(null);
    setNavigating(false);
  };

  // Real-time re-routing: recalculate if the user drifts off the path while navigating.
  useEffect(() => {
    if (!navigating || !selected || !geo.position || !routes) return;
    const off = distanceToPathM(geo.position, routes.primary.coordinates);
    if (off > DEVIATION_M && Date.now() - lastRerouteRef.current > REROUTE_COOLDOWN_MS) {
      lastRerouteRef.current = Date.now();
      loadRoute(geo.position, selected.coords);
    }
  }, [geo.position, navigating, selected, routes]);

  // Displayed route/POIs are gated on a live selection (state may hold stale data).
  const activeRoutes = selected ? routes : null;
  const activePois = selected ? pois : [];
  const routeCoords = activeRoutes?.primary.coordinates ?? null;

  // Live remaining distance / ETA as the user moves along the route.
  const remainingKm = useMemo(() => {
    if (!activeRoutes) return 0;
    if (geo.position && routeCoords) return remainingAlongPathKm(geo.position, routeCoords);
    return activeRoutes.primary.distanceKm;
  }, [activeRoutes, geo.position, routeCoords]);

  const remainingMin =
    mode === 'walking'
      ? walkingMinutes(remainingKm)
      : activeRoutes && activeRoutes.primary.distanceKm > 0
        ? activeRoutes.primary.drivingMin * (remainingKm / activeRoutes.primary.distanceKm)
        : drivingMinutes(remainingKm);

  const startNav = () => {
    setNavigating((n) => {
      const next = !n;
      if (next) setFollow(true);
      return next;
    });
  };

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-3xl font-bold text-ink">Nearby</h1>
          <StatusDot geo={geo} />
        </div>
        <Button
          variant={follow ? 'primary' : 'ghost'}
          size="sm"
          onClick={() => setFollow((f) => !f)}
        >
          {follow ? '📍 Following you' : '📍 Recenter'}
        </Button>
      </div>

      {geo.error && (
        <div className="mb-4 rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-sm text-amber-700">
          {geo.error} Showing apartments near Tarkwa instead.
        </div>
      )}

      <div className="lg:grid lg:h-[calc(100vh-9rem)] lg:grid-cols-[minmax(340px,400px)_1fr] lg:gap-4">
        {/* Panel */}
        <div className="order-2 mt-4 lg:order-1 lg:mt-0 lg:overflow-y-auto lg:pr-1">
          {selected && activeRoutes ? (
            <RoutePanel
              property={selected}
              routes={activeRoutes}
              mode={mode}
              onMode={setMode}
              remainingKm={remainingKm}
              remainingMin={remainingMin}
              navigating={navigating}
              onToggleNav={startNav}
              onBack={clearSelection}
              pois={activePois}
              poiLoading={poiLoading}
            />
          ) : selected && routeLoading ? (
            <p className="text-muted">Calculating the best route…</p>
          ) : (
            <div className="space-y-3">
              <p className="text-sm text-muted">
                {rows.length} apartment{rows.length === 1 ? '' : 's'} sorted by distance
              </p>
              {rows.map((row) => (
                <ApartmentCard key={row.property.id} row={row} onSelect={() => select(row.property.id)} />
              ))}
            </div>
          )}
        </div>

        {/* Map */}
        <div className="order-1 h-[55vh] overflow-hidden rounded-xl border border-gray-200 lg:order-2 lg:h-full">
          <NearbyMap
            user={geo.position}
            accuracy={geo.accuracy}
            apartments={rows}
            selectedId={selectedId}
            onSelect={select}
            routeCoords={routeCoords}
            altRoutes={activeRoutes?.alternatives.map((a) => a.coordinates) ?? []}
            pois={activePois}
            follow={follow}
          />
        </div>
      </div>
    </div>
  );
}
