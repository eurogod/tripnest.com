import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { configApi, notificationsApi, propertiesApi } from './services';
import { useAuth } from '@/auth/AuthContext';
import type { Property } from '@/types/api';

const DEFAULT_MAP = {
  provider: 'OpenStreetMap',
  tileUrl: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
  attribution: '© OpenStreetMap contributors',
  maxZoom: 19,
};

export function useAppConfig() {
  return useQuery({
    queryKey: ['app-config'],
    queryFn: configApi.appInfo,
    staleTime: 5 * 60_000,
  });
}

export function useMapConfig() {
  const { data } = useAppConfig();
  return data?.map ?? DEFAULT_MAP;
}

export function useUnreadCount() {
  const { user } = useAuth();
  return useQuery({
    queryKey: ['unread-count'],
    queryFn: notificationsApi.unreadCount,
    enabled: !!user,
    refetchInterval: 60_000,
  });
}

/** The current host's own listings, with a propertyId → Property lookup for joining bookings/agreements. */
export function useMyProperties() {
  const { user } = useAuth();
  return useQuery({
    queryKey: ['my-properties'],
    queryFn: propertiesApi.mine,
    enabled: !!user,
    staleTime: 60_000,
  });
}

/** propertyId → Property map across all properties the user can see (for titles on bookings/agreements). */
export function usePropertyLookup(source: 'all' | 'mine' = 'all') {
  const { user } = useAuth();
  const query = useQuery({
    queryKey: ['properties', source],
    queryFn: source === 'mine' ? propertiesApi.mine : propertiesApi.list,
    enabled: source === 'all' || !!user,
    staleTime: 60_000,
  });
  const map = useMemo(() => {
    const m = new Map<string, Property>();
    (query.data ?? []).forEach((p) => m.set(p.propertyId, p));
    return m;
  }, [query.data]);
  return map;
}

/** Ghana's major towns for the location picker / map centering. */
export const GH_CENTER: [number, number] = [7.95, -1.03];
export const TOWNS: Record<string, [number, number]> = {
  Accra: [5.6037, -0.187],
  Kumasi: [6.6885, -1.6244],
  Takoradi: [4.8995, -1.7554],
  Tamale: [9.4008, -0.8393],
  'Cape Coast': [5.1053, -1.2466],
  Tarkwa: [5.3006, -1.9889],
  Tema: [5.6698, -0.0166],
  Koforidua: [6.0941, -0.2591],
};
