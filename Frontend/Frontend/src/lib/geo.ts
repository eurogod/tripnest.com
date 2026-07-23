import type { LatLng } from '../types';

const EARTH_RADIUS_KM = 6371;
const toRad = (deg: number) => (deg * Math.PI) / 180;

/** Great-circle distance between two coordinates, in kilometres (Haversine). */
export function haversineKm(a: LatLng, b: LatLng): number {
  const dLat = toRad(b.lat - a.lat);
  const dLng = toRad(b.lng - a.lng);
  const lat1 = toRad(a.lat);
  const lat2 = toRad(b.lat);
  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.sin(dLng / 2) ** 2 * Math.cos(lat1) * Math.cos(lat2);
  return 2 * EARTH_RADIUS_KM * Math.asin(Math.sqrt(h));
}

/** Human distance, e.g. 0.4 -> "400 m", 3.27 -> "3.3 km". */
export function formatDistance(km: number): string {
  if (km < 1) return `${Math.round(km * 1000)} m`;
  return `${km.toFixed(1)} km`;
}

// Average urban speeds (km/h) used to estimate travel time from a distance.
const DRIVING_KMH = 28;
const WALKING_KMH = 4.8;

export function drivingMinutes(km: number): number {
  return (km / DRIVING_KMH) * 60;
}
export function walkingMinutes(km: number): number {
  return (km / WALKING_KMH) * 60;
}

/** Human duration from minutes, e.g. 0.5 -> "<1 min", 92 -> "1h 32m". */
export function formatDuration(minutes: number): string {
  if (minutes < 1) return '<1 min';
  if (minutes < 60) return `${Math.round(minutes)} min`;
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return m === 0 ? `${h}h` : `${h}h ${m}m`;
}

/** Clock time `minutes` from now, e.g. "3:45 PM". */
export function etaClock(minutes: number): string {
  const d = new Date(Date.now() + minutes * 60_000);
  return d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
}

// --- Path geometry (for deviation detection + remaining-distance ETA) --------

/** Approx metres per degree at a given latitude, for planar segment math. */
function metresPerDeg(lat: number): { x: number; y: number } {
  const latRad = toRad(lat);
  return { x: 111_320 * Math.cos(latRad), y: 110_574 };
}

/** Shortest distance (metres) from a point to a segment a–b. */
function pointToSegmentM(p: LatLng, a: LatLng, b: LatLng): number {
  const { x: mx, y: my } = metresPerDeg(p.lat);
  const px = (p.lng - a.lng) * mx;
  const py = (p.lat - a.lat) * my;
  const bx = (b.lng - a.lng) * mx;
  const by = (b.lat - a.lat) * my;
  const len2 = bx * bx + by * by;
  const t = len2 === 0 ? 0 : Math.max(0, Math.min(1, (px * bx + py * by) / len2));
  const dx = px - bx * t;
  const dy = py - by * t;
  return Math.sqrt(dx * dx + dy * dy);
}

/** Smallest distance (metres) from a point to a polyline path. */
export function distanceToPathM(p: LatLng, path: LatLng[]): number {
  if (path.length === 0) return Infinity;
  if (path.length === 1) return haversineKm(p, path[0]) * 1000;
  let min = Infinity;
  for (let i = 0; i < path.length - 1; i++) {
    min = Math.min(min, pointToSegmentM(p, path[i], path[i + 1]));
  }
  return min;
}

/** Index of the path vertex nearest to a point. */
function nearestIndex(p: LatLng, path: LatLng[]): number {
  let best = 0;
  let bestD = Infinity;
  for (let i = 0; i < path.length; i++) {
    const d = haversineKm(p, path[i]);
    if (d < bestD) { bestD = d; best = i; }
  }
  return best;
}

/** Remaining distance (km) along the path from the point nearest to `p`. */
export function remainingAlongPathKm(p: LatLng, path: LatLng[]): number {
  if (path.length < 2) return 0;
  const i = nearestIndex(p, path);
  let km = haversineKm(p, path[i]);
  for (let j = i; j < path.length - 1; j++) km += haversineKm(path[j], path[j + 1]);
  return km;
}
