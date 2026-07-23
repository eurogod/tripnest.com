import type { Listing } from '../types';
import { apiDelete, apiGet, apiGetList, apiPatch, apiPost, apiPut, apiUpload } from './client';
import { mapListing, type ListingCopySuggestionDto, type PropertyResponseDto } from './backend';

export async function getListings(): Promise<Listing[]> {
  const dtos = await apiGetList<PropertyResponseDto>('/api/properties/user/my-properties');
  return dtos.map(mapListing);
}

/** PropertyStatus enum values the backend accepts for a self-serve status change. */
export const LISTING_STATUS_CODE = { draft: 0, active: 1, inactive: 2 } as const;

/**
 * Publish a listing (Active) or take it offline (Inactive) instantly — no admin
 * approval. Returns the updated listing.
 */
export async function setListingStatus(
  propertyId: string,
  status: 'draft' | 'active' | 'inactive',
): Promise<Listing> {
  const dto = await apiPatch<PropertyResponseDto>(`/api/properties/${propertyId}/status`, {
    status: LISTING_STATUS_CODE[status],
  });
  return mapListing(dto);
}

/** Mark one uploaded photo as the listing's cover (primary). Returns the updated listing. */
export async function setListingCover(propertyId: string, photoId: string): Promise<Listing> {
  const dto = await apiPatch<PropertyResponseDto>(`/api/properties/${propertyId}/cover/${photoId}`);
  return mapListing(dto);
}

/** Remove one uploaded photo. Returns the updated listing (cover reassigned if needed). */
export async function removeListingPhoto(propertyId: string, photoId: string): Promise<Listing> {
  const dto = await apiDelete<PropertyResponseDto>(`/api/properties/${propertyId}/photos/${photoId}`);
  return mapListing(dto);
}

/** Full property record (with photos) for the gallery detail page. */
export async function getListingById(propertyId: string): Promise<Listing> {
  const dto = await apiGet<PropertyResponseDto>(`/api/properties/${propertyId}`);
  return mapListing(dto);
}

export interface NewListingInput {
  title: string;
  description: string;
  location: string; 
  bedrooms: number;
  bathrooms: number;
  monthlyRent: number;
  dailyRate?: number;
  propertyType: string;
  stayType: number; // StayType: 0 ShortTerm, 1 LongTerm, 2 Student
  cancellationPolicy: number; // CancellationPolicy: 0 Flexible, 1 Moderate, 2 Strict
  amenities?: string;
}

// Default coordinates (Tarkwa, Ghana) until the form grows a map picker —
// the backend requires lat/lng but nothing user-facing consumes them yet.
const DEFAULT_COORDS = { latitude: 5.3018, longitude: -1.9931 };

export async function createListing(input: NewListingInput): Promise<Listing> {
  const dto = await apiPost<PropertyResponseDto>('/api/properties', {
    ...DEFAULT_COORDS,
    ...input,
    dailyRate: input.dailyRate || null,
    amenities: input.amenities || null,
  });
  return mapListing(dto);
}

/** Full property record for pre-filling the edit form. */
export function getListingProperty(propertyId: string): Promise<PropertyResponseDto> {
  return apiGet<PropertyResponseDto>(`/api/properties/${propertyId}`);
}

/**
 * Update a listing. The backend PUT replaces every field, so callers must
 * send the complete form (pre-filled from getListingProperty) plus the
 * original coordinates.
 */
export async function updateListing(
  propertyId: string,
  input: NewListingInput & { latitude: number; longitude: number },
): Promise<Listing> {
  const dto = await apiPut<PropertyResponseDto>(`/api/properties/${propertyId}`, {
    ...input,
    dailyRate: input.dailyRate || null,
    amenities: input.amenities || null,
  });
  return mapListing(dto);
}

export type ListingCopySuggestion = ListingCopySuggestionDto;

/**
 * AI-drafted title/description/highlights from the listing's facts and photos
 * (owner only; the property must already have photos). Advisory — the host
 * reviews and applies the copy manually.
 */
export function generateListingCopy(propertyId: string): Promise<ListingCopySuggestion> {
  return apiPost<ListingCopySuggestionDto>(`/api/properties/${propertyId}/generate-copy`);
}

/**
 * Upload listing photos (owner only). Returns the stored photo paths.
 * These photos also become the source material for the property's
 * generated video walkthrough.
 */
export function uploadListingPhotos(propertyId: string, files: File[]): Promise<string[]> {
  const form = new FormData();
  for (const file of files) form.append('files', file, file.name);
  return apiUpload<string[]>(`/api/properties/${propertyId}/photos`, form);
}
