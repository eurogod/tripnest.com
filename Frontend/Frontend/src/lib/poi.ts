
export type PoiCategory =
  | 'hospital' | 'pharmacy' | 'restaurant' | 'shopping' | 'school' | 'bus_stop' | 'fuel';

export interface Poi {
  id: string;
  name: string;
  category: PoiCategory;
  coords: LatLng;
}

export const POI_META: Record<PoiCategory, { label: string; color: string; emoji: string }> = {
  hospital: { label: 'Hospitals', color: '#ef4444', emoji: '🏥' },
  pharmacy: { label: 'Pharmacies', color: '#10b981', emoji: '💊' },
  restaurant: { label: 'Restaurants', color: '#f59e0b', emoji: '🍽️' },
  shopping: { label: 'Shopping', color: '#8b5cf6', emoji: '🛍️' },
  school: { label: 'Schools', color: '#3b82f6', emoji: '🏫' },
  bus_stop: { label: 'Bus stops', color: '#0ea5e9', emoji: '🚌' },
  fuel: { label: 'Fuel', color: '#6b7280', emoji: '⛽' },
};

// Overpass tag filters per category. Each entry becomes a node/way query clause.
const FILTERS: Record<PoiCategory, string[]> = {
  hospital: ['amenity=hospital', 'amenity=clinic'],
  pharmacy: ['amenity=pharmacy'],
  restaurant: ['amenity=restaurant', 'amenity=cafe', 'amenity=fast_food'],
  shopping: ['shop=mall', 'shop=supermarket', 'shop=department_store'],
  school: ['amenity=school', 'amenity=college', 'amenity=university'],
  bus_stop: ['highway=bus_stop', 'amenity=bus_station'],
  fuel: ['amenity=fuel'],
};

function categoryFor(tags: Record<string, string>): PoiCategory | null {
  for (const [cat, clauses] of Object.entries(FILTERS) as [PoiCategory, string[]][]) {
    for (const clause of clauses) {
      const [k, v] = clause.split('=');
      if (tags[k] === v) return cat;
    }
  }
  return null;
}

interface OverpassElement {
  type: string;
  id: number;
  lat?: number;
  lon?: number;
  center?: { lat: number; lon: number };
  tags?: Record<string, string>;
}

/**
 * Fetch points of interest within `radiusM` of a coordinate from the Overpass
 * (OpenStreetMap) API. Returns an empty list if the service is unavailable.
 */
export async function getNearbyPois(center: LatLng, radiusM = 1500): Promise<Poi[]> {
  const around = `(around:${radiusM},${center.lat},${center.lng})`;
  const clauses = Object.values(FILTERS)
    .flat()
    .flatMap((c) => {
      const [k, v] = c.split('=');
      return [`node["${k}"="${v}"]${around};`, `way["${k}"="${v}"]${around};`];
    })
    .join('');
  const query = `[out:json][timeout:20];(${clauses});out center 60;`;

  try {
    const res = await fetch('https://overpass-api.de/api/interpreter', {
      method: 'POST',
      body: query,
    });
    if (!res.ok) throw new Error(`Overpass ${res.status}`);
    const data = (await res.json()) as { elements?: OverpassElement[] };
    const seen = new Set<string>();
    const pois: Poi[] = [];
    for (const el of data.elements ?? []) {
      const tags = el.tags ?? {};
      const category = categoryFor(tags);
      const lat = el.lat ?? el.center?.lat;
      const lng = el.lon ?? el.center?.lon;
      if (!category || lat == null || lng == null) continue;
      const name = tags.name ?? POI_META[category].label.replace(/s$/, '');
      const key = `${category}:${name}:${lat.toFixed(4)}`;
      if (seen.has(key)) continue;
      seen.add(key);
      pois.push({ id: `${el.type}-${el.id}`, name, category, coords: { lat, lng } });
    }
    return pois;
  } catch {
    return [];
  }
}