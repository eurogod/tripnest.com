import type { LatLng } from '../types';
import { haversineKm, drivingMinutes, walkingMinutes } from './geo';

export interface RouteStep {
  instruction: string;
  distanceM: number;
  name: string;
}

export interface RouteResult {
  coordinates: LatLng[];
  distanceKm: number;
  drivingMin: number;
  walkingMin: number;
  steps: RouteStep[];
  /** 'osrm' = real road route; 'fallback' = straight-line estimate. */
  source: 'osrm' | 'fallback';
}

export interface RoutesResponse {
  primary: RouteResult;
  alternatives: RouteResult[];
}

const OSRM = 'https://router.project-osrm.org/route/v1/driving';

interface OsrmStep {
  name?: string;
  distance: number;
  maneuver: { type: string; modifier?: string };
}
interface OsrmRoute {
  geometry: { coordinates: [number, number][] };
  distance: number;
  duration: number;
  legs: { steps: OsrmStep[] }[];
}

/** Turn a raw OSRM maneuver into a readable turn-by-turn instruction. */
function describe(step: OsrmStep): string {
  const { type, modifier } = step.maneuver;
  const road = step.name ? ` onto ${step.name}` : '';
  switch (type) {
    case 'depart': return step.name ? `Head out on ${step.name}` : 'Start heading toward your destination';
    case 'arrive': return 'Arrive at your destination';
    case 'roundabout':
    case 'rotary': return `Take the roundabout${road}`;
    case 'merge': return `Merge${road}`;
    case 'fork': return `Keep ${modifier ?? 'ahead'} at the fork${road}`;
    case 'on ramp': return `Take the ramp${road}`;
    case 'off ramp': return `Take the exit${road}`;
    case 'continue': return `Continue${modifier ? ` ${modifier}` : ''}${road}`;
    case 'new name': return `Continue${road}`;
    case 'end of road': return `Turn ${modifier ?? 'ahead'}${road}`;
    default: return `Turn ${modifier ?? 'ahead'}${road}`;
  }
}

function toResult(route: OsrmRoute): RouteResult {
  const coordinates = route.geometry.coordinates.map(([lng, lat]) => ({ lat, lng }));
  const distanceKm = route.distance / 1000;
  const steps: RouteStep[] = route.legs
    .flatMap((leg) => leg.steps)
    .map((s) => ({ instruction: describe(s), distanceM: s.distance, name: s.name ?? '' }))
    .filter((s, i, arr) => i === 0 || s.instruction !== arr[i - 1].instruction);
  return {
    coordinates,
    distanceKm,
    drivingMin: route.duration / 60,
    walkingMin: walkingMinutes(distanceKm),
    steps,
    source: 'osrm',
  };
}

/** Straight-line estimate used when the routing service is unreachable. */
function fallbackRoute(from: LatLng, to: LatLng): RouteResult {
  const distanceKm = haversineKm(from, to);
  return {
    coordinates: [from, to],
    distanceKm,
    drivingMin: drivingMinutes(distanceKm),
    walkingMin: walkingMinutes(distanceKm),
    steps: [
      { instruction: 'Head toward your destination', distanceM: distanceKm * 1000, name: '' },
      { instruction: 'Arrive at your destination', distanceM: 0, name: '' },
    ],
    source: 'fallback',
  };
}

/**
 * Fetch driving directions (geometry, distance, ETA, turn-by-turn, alternatives)
 * from the public OSRM service. Falls back to a straight-line estimate offline.
 */
export async function getRoutes(from: LatLng, to: LatLng): Promise<RoutesResponse> {
  const url =
    `${OSRM}/${from.lng},${from.lat};${to.lng},${to.lat}` +
    `?overview=full&geometries=geojson&steps=true&alternatives=true`;
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`OSRM ${res.status}`);
    const data = (await res.json()) as { routes?: OsrmRoute[] };
    if (!data.routes?.length) throw new Error('No route');
    const [primary, ...rest] = data.routes.map(toResult);
    return { primary, alternatives: rest.slice(0, 2) };
  } catch {
    return { primary: fallbackRoute(from, to), alternatives: [] };
  }
}
