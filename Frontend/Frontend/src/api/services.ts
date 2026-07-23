import type { ServiceProvider } from '../types';
import { apiGetList, apiPost } from './client';
import { getProperties } from './properties';

// ---------------------------------------------------------------------------
// Service directory (Caretakers / House Help / Agents). Agents and caretakers
// come from TripNest.Core and carry a userId, which unlocks the real request +
// chat flows. House Help has no backend concept yet, so that directory shows
// its empty state until Core grows one.
// ---------------------------------------------------------------------------

interface AgentDto {
  agentId: string;
  userId: string;
  licenseNumber: string;
  phoneNumber: string;
  bio: string;
  status: number;
  commissionRate?: number | null;
  yearsOfExperience?: number | null;
  joinDate: string;
  certifications?: string | null;
}

interface CaretakerDto {
  caretakerId: string;
  userId: string;
  propertyId: string;
  status: number;
  startDate: string;
  endDate?: string | null;
  monthlyCompensation?: number | null;
  responsibilities: string;
}

export interface ServiceRequestDto {
  serviceRequestId: string;
  caretakerId: string;
  requestedByUserId: string;
  propertyId: string;
  serviceType: string;
  description: string;
  status: string;
  rating?: number | null;
  reviewComment?: string | null;
  createdAt: string;
  completedAt?: string | null;
}

export interface ViewingRequestDto {
  viewingRequestId: string;
  agentId: string;
  tenantId: string;
  propertyId: string;
  scheduledAt: string;
  notes?: string | null;
  status: string;
  createdAt: string;
}

// The list DTOs don't expose the person's name yet, so live rows get
// descriptive names until the backend adds FullName to these responses.
function mapAgent(dto: AgentDto): ServiceProvider {
  return {
    id: dto.agentId,
    userId: dto.userId,
    name: `Verified Agent · ${dto.licenseNumber}`,
    category: 'Agents',
    role: dto.yearsOfExperience ? `${dto.yearsOfExperience} yrs experience` : 'Verified Agent',
    // Agent DTOs carry no location — the directory shows the service area.
    location: 'Ghana',
    rating: 0,
    reviews: 0,
    verified: true,
    rate: 0,
    ratePeriod: 'commission',
    skills: dto.certifications ? dto.certifications.split(',').map((s) => s.trim()) : ['Property viewings'],
    bio: dto.bio,
  };
}

function mapCaretaker(dto: CaretakerDto, location?: string): ServiceProvider {
  return {
    id: dto.caretakerId,
    userId: dto.userId,
    name: 'Property Caretaker',
    category: 'Caretakers',
    role: 'Resident caretaker',
    location: location ?? 'Ghana',
    rating: 0,
    reviews: 0,
    verified: true,
    rate: dto.monthlyCompensation ?? 0,
    ratePeriod: 'month',
    skills: dto.responsibilities.split(',').map((s) => s.trim()).filter(Boolean),
    bio: dto.responsibilities,
  };
}

/** userId → directory display name/role for live agents and caretakers. */
export async function getProviderDirectory(): Promise<Record<string, { name: string; role: string }>> {
  const [agents, caretakers] = await Promise.all([
    apiGetList<AgentDto>('/api/agents').catch(() => [] as AgentDto[]),
    apiGetList<CaretakerDto>('/api/caretakers').catch(() => [] as CaretakerDto[]),
  ]);
  const map: Record<string, { name: string; role: string }> = {};
  for (const a of agents) map[a.userId] = { name: `Verified Agent · ${a.licenseNumber}`, role: 'Agent' };
  for (const c of caretakers) map[c.userId] = { name: 'Property Caretaker', role: 'Caretaker' };
  return map;
}

/** propertyId → caretaker userId, for "chat about this property" flows. */
export async function getCaretakersByProperty(): Promise<Record<string, string>> {
  try {
    const dtos = await apiGetList<CaretakerDto>('/api/caretakers');
    return Object.fromEntries(dtos.map((c) => [c.propertyId, c.userId]));
  } catch {
    return {};
  }
}

async function liveProviders(category: string): Promise<ServiceProvider[]> {
  try {
    if (category === 'Agents') {
      const dtos = await apiGetList<AgentDto>('/api/agents');
      return dtos.map(mapAgent);
    }
    if (category === 'Caretakers') {
      // A caretaker's location is their assigned property's location.
      const [dtos, properties] = await Promise.all([
        apiGetList<CaretakerDto>('/api/caretakers'),
        getProperties().catch(() => []),
      ]);
      const locations = new Map(properties.map((p) => [p.id, p.location]));
      return dtos.map((dto) => mapCaretaker(dto, locations.get(dto.propertyId)));
    }
  } catch {
    // API down — the directory shows its empty state rather than fake people.
  }
  return [];
}

export async function getProviders(category: string): Promise<ServiceProvider[]> {
  return liveProviders(category);
}

export async function getProviderById(id: string): Promise<ServiceProvider | undefined> {
  for (const category of ['Caretakers', 'Agents']) {
    const rows = await getProviders(category);
    const found = rows.find((p) => p.id === id);
    if (found) return found;
  }
  return undefined;
}

/** Ask a caretaker / house help for a service. Live providers only. */
export function requestCaretakerService(input: {
  caretakerId: string;
  serviceType: string;
  description: string;
  propertyId?: string;
  scheduledFor?: string;
}): Promise<ServiceRequestDto> {
  return apiPost<ServiceRequestDto>('/api/caretakers/service-requests', input);
}

/** Ask an agent for a property viewing. Live providers only. */
export function requestAgentViewing(
  agentId: string,
  input: { propertyId: string; scheduledAt: string; notes?: string },
): Promise<ViewingRequestDto> {
  return apiPost<ViewingRequestDto>(`/api/agents/${agentId}/viewing-requests`, input);
}
