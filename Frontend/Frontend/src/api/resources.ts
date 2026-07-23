import type { Resource } from '../types';
import { apiGetList } from './client';
import { mapResource, type ResourceResponseDto } from './backend';

// Host resource library (guides, policies, templates, videos), backed by
// TripNest.Core's /api/resources endpoint.

export async function getResources(): Promise<Resource[]> {
  const dtos = await apiGetList<ResourceResponseDto>('/api/resources');
  return dtos.map(mapResource);
}
