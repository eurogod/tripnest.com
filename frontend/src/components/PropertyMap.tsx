import { useMemo } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import { useMapConfig } from '@/lib/hooks';
import { money } from '@/lib/format';

function pricePin(label: string, active: boolean) {
  return L.divIcon({
    className: '',
    html: `<div class="tn-price-pin ${active ? 'active' : ''}">${label}</div>`,
    iconSize: [0, 0],
    iconAnchor: [0, 0],
  });
}

function dotPin() {
  return L.divIcon({
    className: '',
    html: `<div style="width:18px;height:18px;border-radius:50%;background:#0f766e;border:3px solid #fff;box-shadow:0 1px 6px rgba(0,0,0,.4)"></div>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
  });
}

function Recenter({ center, zoom }: { center: [number, number]; zoom: number }) {
  const map = useMap();
  map.setView(center, zoom);
  return null;
}

export interface MapPoint {
  id: string;
  lat: number;
  lng: number;
  price?: number;
  title: string;
}

/** Single-location map (property detail / pin display). */
export function SingleMap({ lat, lng, title, zoom = 14 }: { lat: number; lng: number; title: string; zoom?: number }) {
  const cfg = useMapConfig();
  return (
    <MapContainer center={[lat, lng]} zoom={zoom} scrollWheelZoom={false} className="h-full w-full">
      <TileLayer url={cfg.tileUrl} attribution={cfg.attribution} maxZoom={cfg.maxZoom} />
      <Marker position={[lat, lng]} icon={dotPin()}>
        <Popup>{title}</Popup>
      </Marker>
    </MapContainer>
  );
}

/** Search results map with price pins synced to the listing under the cursor. */
export function ResultsMap({
  points,
  activeId,
  onSelect,
}: {
  points: MapPoint[];
  activeId?: string | null;
  onSelect?: (id: string) => void;
}) {
  const cfg = useMapConfig();
  const center = useMemo<[number, number]>(() => {
    if (!points.length) return [7.95, -1.03];
    const lat = points.reduce((s, p) => s + p.lat, 0) / points.length;
    const lng = points.reduce((s, p) => s + p.lng, 0) / points.length;
    return [lat, lng];
  }, [points]);

  return (
    <MapContainer center={center} zoom={points.length ? 12 : 7} scrollWheelZoom className="h-full w-full">
      <TileLayer url={cfg.tileUrl} attribution={cfg.attribution} maxZoom={cfg.maxZoom} />
      <Recenter center={center} zoom={points.length ? 12 : 7} />
      {points.map((p) => (
        <Marker
          key={p.id}
          position={[p.lat, p.lng]}
          icon={p.price ? pricePin(money(p.price), p.id === activeId) : dotPin()}
          eventHandlers={{ click: () => onSelect?.(p.id) }}
        >
          <Popup>{p.title}</Popup>
        </Marker>
      ))}
    </MapContainer>
  );
}
