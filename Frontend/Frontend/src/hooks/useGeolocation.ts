import { useEffect, useRef, useState } from 'react';
import type { LatLng } from '../types';

export type GeoStatus = 'prompt' | 'locating' | 'granted' | 'denied' | 'unavailable';

export interface GeoState {
  position: LatLng | null;
  accuracy: number | null;
  heading: number | null;
  status: GeoStatus;
  error: string | null;
}

/**
 * Continuously track the device location via the Geolocation API.
 * Requests permission on mount and keeps a live `watchPosition` running so the
 * map and distances update in real time as the user moves.
 */
export function useGeolocation(enabled = true): GeoState {
  const [state, setState] = useState<GeoState>(() => {
    const supported = typeof navigator !== 'undefined' && 'geolocation' in navigator;
    return {
      position: null,
      accuracy: null,
      heading: null,
      status: supported ? 'locating' : 'unavailable',
      error: supported ? null : 'Geolocation is not supported on this device.',
    };
  });
  const watchId = useRef<number | null>(null);

  useEffect(() => {
    if (!enabled || !('geolocation' in navigator)) return;

    const onPos = (pos: GeolocationPosition) =>
      setState({
        position: { lat: pos.coords.latitude, lng: pos.coords.longitude },
        accuracy: pos.coords.accuracy,
        heading: Number.isFinite(pos.coords.heading) ? pos.coords.heading : null,
        status: 'granted',
        error: null,
      });

    const onErr = (err: GeolocationPositionError) =>
      setState((s) => ({
        ...s,
        status: err.code === err.PERMISSION_DENIED ? 'denied' : 'unavailable',
        error:
          err.code === err.PERMISSION_DENIED
            ? 'Location permission denied. Enable it to see apartments near you.'
            : 'Could not determine your location.',
      }));

    const opts: PositionOptions = { enableHighAccuracy: true, maximumAge: 5_000, timeout: 15_000 };
    navigator.geolocation.getCurrentPosition(onPos, onErr, opts);
    watchId.current = navigator.geolocation.watchPosition(onPos, onErr, opts);

    return () => {
      if (watchId.current != null) navigator.geolocation.clearWatch(watchId.current);
    };
  }, [enabled]);

  return state;
}
