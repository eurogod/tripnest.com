import { useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Circle, Polyline, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import type { LatLng, Property } from '../../../types';
import { type Poi, POI_META } from '../../../lib/poi';
import { formatCedi } from '../../../lib/format';
import { formatDistance } from '../../../lib/geo';
import { apartmentIcon, poiIcon, userIcon } from './markers';

export interface AptWithDistance {
  property: Property;
  distanceKm: number;
}

interface NearbyMapProps {
  user: LatLng | null;
  accuracy: number | null;
  apartments: AptWithDistance[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  routeCoords: LatLng[] | null;
  altRoutes: LatLng[][];
  pois: Poi[];
  follow: boolean;
}

const TARKWA: LatLng = { lat: 5.3018, lng: -1.993 };

/** Pan to the live location whenever it changes while "follow" is on. */
function FollowUser({ user, follow }: { user: LatLng | null; follow: boolean }) {
  const map = useMap();
  useEffect(() => {
    if (follow && user) map.panTo([user.lat, user.lng], { animate: true });
  }, [user, follow, map]);
  return null;
}

/** Fit the viewport to the active route when it (re)loads. */
function FitRoute({ coords }: { coords: LatLng[] | null }) {
  const map = useMap();
  useEffect(() => {
    if (coords && coords.length > 1) {
      map.fitBounds(L.latLngBounds(coords.map((c) => [c.lat, c.lng])), { padding: [48, 48] });
    }
  }, [coords, map]);
  return null;
}

export default function NearbyMap({
  user, accuracy, apartments, selectedId, onSelect, routeCoords, altRoutes, pois, follow,
}: NearbyMapProps) {
  const center = user ?? apartments[0]?.property.coords ?? TARKWA;

  return (
    <MapContainer
      center={[center.lat, center.lng]}
      zoom={14}
      scrollWheelZoom
      className="h-full w-full"
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      <FollowUser user={user} follow={follow} />
      <FitRoute coords={routeCoords} />

      {user && (
        <>
          {accuracy != null && accuracy < 500 && (
            <Circle center={[user.lat, user.lng]} radius={accuracy} pathOptions={{ color: '#2563eb', weight: 1, fillOpacity: 0.08 }} />
          )}
          <Marker position={[user.lat, user.lng]} icon={userIcon()} zIndexOffset={1000}>
            <Popup>You are here</Popup>
          </Marker>
        </>
      )}

      {apartments.map(({ property, distanceKm }) => (
        <Marker
          key={property.id}
          position={[property.coords.lat, property.coords.lng]}
          icon={apartmentIcon(formatCedi(property.price).replace('GH₵ ', '₵'), property.id === selectedId)}
          zIndexOffset={property.id === selectedId ? 800 : 0}
          eventHandlers={{ click: () => onSelect(property.id) }}
        >
          <Popup>
            <span className="block font-semibold">{property.title}</span>
            <span className="block text-xs">{property.location}</span>
            <span className="block text-xs">{formatDistance(distanceKm)} away · ⭐ {property.rating}</span>
          </Popup>
        </Marker>
      ))}

      {pois.map((p) => (
        <Marker key={p.id} position={[p.coords.lat, p.coords.lng]} icon={poiIcon(POI_META[p.category].color, POI_META[p.category].emoji)}>
          <Popup>
            <span className="block font-semibold">{p.name}</span>
            <span className="block text-xs">{POI_META[p.category].label}</span>
          </Popup>
        </Marker>
      ))}

      {altRoutes.map((alt, i) => (
        <Polyline key={i} positions={alt.map((c) => [c.lat, c.lng])} pathOptions={{ color: '#94a3b8', weight: 4, opacity: 0.7, dashArray: '6 8' }} />
      ))}

      {routeCoords && routeCoords.length > 1 && (
        <Polyline positions={routeCoords.map((c) => [c.lat, c.lng])} pathOptions={{ color: '#0f5132', weight: 6, opacity: 0.9 }} />
      )}
    </MapContainer>
  );
}
