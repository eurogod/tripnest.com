import type { Property } from '../types';
import { apiDelete, apiGet, apiGetList, apiPost } from './client';
import { mapProperty, type PropertyResponseDto, type WishlistItemDto } from './backend';

export async function getProperties(): Promise<Property[]> {
  const dtos = await apiGetList<PropertyResponseDto>('/api/properties');
  return dtos.map(mapProperty);
}

export async function getFeaturedProperties(): Promise<Property[]> {
  const all = await getProperties();
  return all.slice(0, 4);
}

export async function getPropertyById(id: string): Promise<Property | undefined> {
  try {
    const dto = await apiGet<PropertyResponseDto>(`/api/properties/${id}`);
    return mapProperty(dto);
  } catch {
    return undefined;
  }
}

export async function searchProperties(location: string): Promise<Property[]> {
  const dtos = await apiGetList<PropertyResponseDto>(
    `/api/properties/search?location=${encodeURIComponent(location)}`,
  );
  return dtos.map(mapProperty);
}

/** Wishlist-backed "Saved" list: ids from /api/wishlist joined to listings. */
export async function getSavedProperties(): Promise<Property[]> {
  const [items, all] = await Promise.all([
    apiGetList<WishlistItemDto>('/api/wishlist/mine'),
    getProperties(),
  ]);
  const saved = new Set(items.map((i) => i.propertyId));
  return all.filter((p) => saved.has(p.id));
}

export function saveProperty(propertyId: string): Promise<unknown> {
  return apiPost(`/api/wishlist/${propertyId}`);
}

export function unsaveProperty(propertyId: string): Promise<unknown> {
  return apiDelete(`/api/wishlist/${propertyId}`);
}
